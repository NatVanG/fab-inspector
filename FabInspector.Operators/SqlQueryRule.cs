using FabInspector.Core;
using FabInspector.Core.Inspection;
using FabInspector.Core.Part;
using Json.Logic;
using Json.More;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FabInspector.Operators;

/// <summary>
/// Handles the `sqlquery` operation.
/// Retrieves a Lakehouse SQL endpoint through the Fabric REST API and executes
/// a T-SQL query expected to return JSON output using FOR JSON.
/// </summary>
[Operator("sqlquery")]
[JsonConverter(typeof(SqlQueryJsonConverter))]
public class SqlQueryRule : Json.Logic.Rule
{
    private const string FabricApiBaseUrl = "https://api.fabric.microsoft.com/v1";
    private static readonly string[] SqlDatabaseScopes = new[] { "https://database.windows.net/.default" };
    private static readonly Regex StartsWithSelectOrWithRegex = new(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ForbiddenKeywordRegex = new(@"\b(CREATE|ALTER|DROP)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CommentTokenRegex = new(@"(--|/\*|\*/)", RegexOptions.Compiled);

    internal Json.Logic.Rule Query { get; }
    internal Json.Logic.Rule WorkspaceId { get; }
    internal Json.Logic.Rule FabricItem { get; }
    internal Json.Logic.Rule? RefreshMetadata { get; }
    internal Json.Logic.Rule? RecreateTables { get; }

    internal SqlQueryRule(
        Json.Logic.Rule query,
        Json.Logic.Rule workspaceId,
        Json.Logic.Rule fabricItem,
        Json.Logic.Rule? refreshMetadata,
        Json.Logic.Rule? recreateTables)
    {
        Query = query;
        WorkspaceId = workspaceId;
        FabricItem = fabricItem;
        RefreshMetadata = refreshMetadata;
        RecreateTables = recreateTables;
    }

    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var stopwatch = Stopwatch.StartNew();

        var queryValue = Query.Apply(data, contextData);
        var sqlQuery = queryValue?.Stringify();
        if (string.IsNullOrWhiteSpace(sqlQuery))
            throw new JsonLogicException("The sqlquery rule requires a non-empty T-SQL query.");
        ValidateSelectOnlyQuery(sqlQuery);

        var workspaceId = WorkspaceId.Apply(data, contextData)?.Stringify();
        workspaceId = workspaceId!.Equals(Utils.Constants.ContextFabricWorkspace, StringComparison.OrdinalIgnoreCase)
            ? InspectionContextHolder.Require("sqlquery").FabricWorkspaceId
            : workspaceId;
        if (string.IsNullOrWhiteSpace(workspaceId) || !Guid.TryParse(workspaceId, out _))
            throw new InvalidOperationException("WorkspaceId is not configured.");

        var lakehouseId = FabricItem.Apply(data, contextData)?.Stringify();
        lakehouseId = lakehouseId!.Equals(Utils.Constants.ContextFabricItem, StringComparison.OrdinalIgnoreCase)
            ? InspectionContextHolder.Require("sqlquery").FabricItem
            : lakehouseId;
        if (string.IsNullOrWhiteSpace(lakehouseId) || !Guid.TryParse(lakehouseId, out _))
            throw new InvalidOperationException("Fabric item must be a Lakehouse ID.");

        var refreshMetadata = ResolveOptionalBooleanParameter(RefreshMetadata, "refreshMetadata", data, contextData, defaultValue: false);
        var recreateTables = ResolveOptionalBooleanParameter(RecreateTables, "recreateTables", data, contextData, defaultValue: false);

        var ctx = InspectionContextHolder.Require("sqlquery");
        var httpClient = ctx.HttpClient;
        var tokenProvider = ctx.TokenProvider;

        var lakehouseUrl = $"{FabricApiBaseUrl}/workspaces/{Uri.EscapeDataString(workspaceId)}/lakehouses/{Uri.EscapeDataString(lakehouseId)}";

        InspectionContextHolder.ReportOperatorProgress("sqlquery", $"Resolving SQL endpoint for lakehouse '{lakehouseId}' in workspace '{workspaceId}'.");

        var metadataRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            lakehouseUrl,
            tokenProvider,
            AuthenticationHelper.FabricItemsApiScopes).ConfigureAwait(false).GetAwaiter().GetResult();

        var metadataResponse = httpClient.SendAsync(metadataRequest).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!metadataResponse.IsSuccessStatusCode)
        {
            var errorContent = metadataResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            throw new HttpRequestException($"Lakehouse metadata request failed ({metadataResponse.StatusCode}): {errorContent}");
        }

        var metadataJson = metadataResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        var metadataNode = JsonNode.Parse(metadataJson)
            ?? throw new InvalidOperationException("Lakehouse metadata response was empty.");

        var endpointConnectionString = metadataNode["properties"]?["sqlEndpointProperties"]?["connectionString"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(endpointConnectionString))
            throw new InvalidOperationException("Lakehouse SQL endpoint connection string was not found in the REST response.");

        if (refreshMetadata)
        {
            var sqlEndpointId = ResolveSqlEndpointId(metadataNode);
            InspectionContextHolder.ReportOperatorProgress("sqlquery", $"Refreshing SQL endpoint metadata for endpoint '{sqlEndpointId}' (recreateTables={recreateTables.ToString().ToLowerInvariant()}).");
            RefreshSqlEndpointMetadata(httpClient, tokenProvider, workspaceId, sqlEndpointId, recreateTables);
            InspectionContextHolder.ReportOperatorProgress("sqlquery", "SQL endpoint metadata refresh completed.");
        }

        var connectionString = BuildConnectionString(endpointConnectionString);
        var queryWithForJson = EnsureForJson(sqlQuery);

        InspectionContextHolder.ReportOperatorProgress("sqlquery", "Executing SQL query through Lakehouse SQL endpoint.");

        try
        {
            var sqlToken = tokenProvider.GetTokenAsync(SqlDatabaseScopes).ConfigureAwait(false).GetAwaiter().GetResult();

            using var connection = new SqlConnection(connectionString)
            {
                AccessToken = sqlToken
            };
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = queryWithForJson;
            command.CommandTimeout = 120;

            var payload = command.ExecuteScalar();
            var payloadText = payload is DBNull || payload == null ? "null" : payload.ToString();
            if (string.IsNullOrWhiteSpace(payloadText))
                payloadText = "null";

            var parsedPayload = JsonNode.Parse(payloadText);

            InspectionContextHolder.ReportOperatorProgress("sqlquery", $"Completed SQL query execution in {stopwatch.ElapsedMilliseconds} ms.");
            return parsedPayload;
        }
        catch (SqlException ex)
        {
            InspectionContextHolder.ReportOperatorProgress("sqlquery", $"SQL query execution failed after {stopwatch.ElapsedMilliseconds} ms.");
            throw new InvalidOperationException($"SQL query execution failed: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            InspectionContextHolder.ReportOperatorProgress("sqlquery", $"JSON parsing failed after {stopwatch.ElapsedMilliseconds} ms.");
            throw new InvalidOperationException("The SQL query did not return a valid JSON payload.", ex);
        }
    }

    private static string ResolveSqlEndpointId(JsonNode metadataNode)
    {
        var sqlEndpointId = metadataNode["properties"]?["sqlEndpointProperties"]?["id"]?.GetValue<string>()
            ?? metadataNode["properties"]?["sqlEndpointProperties"]?["sqlEndpointId"]?.GetValue<string>()
            ?? metadataNode["sqlEndpointId"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(sqlEndpointId) || !Guid.TryParse(sqlEndpointId, out _))
            throw new InvalidOperationException("Lakehouse SQL endpoint ID was not found in the REST response.");

        return sqlEndpointId;
    }

    private static void RefreshSqlEndpointMetadata(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        string workspaceId,
        string sqlEndpointId,
        bool recreateTables)
    {
        var refreshUrl = $"{FabricApiBaseUrl}/workspaces/{Uri.EscapeDataString(workspaceId)}/sqlEndpoints/{Uri.EscapeDataString(sqlEndpointId)}/refreshMetadata";
        var requestBody = JsonSerializer.Serialize(new { recreateTables });
        var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        var refreshRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Post,
            refreshUrl,
            tokenProvider,
            AuthenticationHelper.FabricItemsApiScopes,
            content).ConfigureAwait(false).GetAwaiter().GetResult();

        var refreshResponse = httpClient.SendAsync(refreshRequest).ConfigureAwait(false).GetAwaiter().GetResult();

        if (refreshResponse.StatusCode == HttpStatusCode.Accepted)
        {
            WaitForRefreshOperationCompletion(httpClient, tokenProvider, refreshResponse);
            return;
        }

        if (!refreshResponse.IsSuccessStatusCode)
        {
            var errorContent = refreshResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            throw new HttpRequestException($"SQL endpoint metadata refresh failed ({refreshResponse.StatusCode}): {errorContent}");
        }
    }

    private static void WaitForRefreshOperationCompletion(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        HttpResponseMessage initialResponse)
    {
        var locationHeader = initialResponse.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(locationHeader) &&
            initialResponse.Headers.TryGetValues("Location", out var locationValues))
        {
            foreach (var value in locationValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    locationHeader = value;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(locationHeader))
            throw new HttpRequestException("SQL endpoint metadata refresh returned 202 Accepted but no Location header for polling.");

        var pollingUri = new Uri(locationHeader, UriKind.RelativeOrAbsolute);
        if (!pollingUri.IsAbsoluteUri)
            pollingUri = new Uri(new Uri(FabricApiBaseUrl), locationHeader);

        var pollingUrl = pollingUri.ToString();
        var retryDelayMs = GetRetryAfterMilliseconds(initialResponse, defaultDelayMs: 2_000);

        const int maxAttempts = 60;
        const int maxRetryDelayMs = 10_000;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(retryDelayMs);

            var pollRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
                HttpMethod.Get,
                pollingUrl,
                tokenProvider,
                AuthenticationHelper.FabricItemsApiScopes).ConfigureAwait(false).GetAwaiter().GetResult();

            var pollResponse = httpClient.SendAsync(pollRequest).ConfigureAwait(false).GetAwaiter().GetResult();
            var responseBody = pollResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            if (!pollResponse.IsSuccessStatusCode)
                throw new HttpRequestException($"SQL endpoint metadata refresh status check failed ({pollResponse.StatusCode}): {responseBody}");

            var statusNode = string.IsNullOrWhiteSpace(responseBody) ? null : JsonNode.Parse(responseBody);
            var status = statusNode?["status"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(status) || status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
                return;

            if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase) || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException($"SQL endpoint metadata refresh did not complete successfully. Status: {status}. Response: {responseBody}");

            retryDelayMs = GetRetryAfterMilliseconds(pollResponse, Math.Min(retryDelayMs * 2, maxRetryDelayMs));
        }

        throw new TimeoutException($"Timed out waiting for SQL endpoint metadata refresh after {maxAttempts} polling attempts.");
    }

    private static int GetRetryAfterMilliseconds(HttpResponseMessage response, int defaultDelayMs)
    {
        if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            foreach (var value in retryAfterValues)
            {
                if (int.TryParse(value, out var retryAfterSeconds) && retryAfterSeconds > 0)
                    return retryAfterSeconds * 1_000;
            }
        }

        return defaultDelayMs;
    }

    private static bool ResolveOptionalBooleanParameter(
        Json.Logic.Rule? parameterRule,
        string parameterName,
        JsonNode? data,
        JsonNode? contextData,
        bool defaultValue)
    {
        if (parameterRule == null)
            return defaultValue;

        var parameterValue = parameterRule.Apply(data, contextData);
        if (parameterValue == null)
            return defaultValue;

        try
        {
            return parameterValue.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            var parameterText = parameterValue.Stringify();
            if (bool.TryParse(parameterText, out var parsedBoolean))
                return parsedBoolean;

            throw new JsonLogicException($"The sqlquery rule parameter '{parameterName}' must evaluate to a boolean value.");
        }
    }

    private static string EnsureForJson(string query)
    {
        if (Regex.IsMatch(query, @"\bFOR\s+JSON\b", RegexOptions.IgnoreCase))
            return query;

        var trimmed = query.TrimEnd();
        if (trimmed.EndsWith(";", StringComparison.Ordinal))
            trimmed = trimmed[..^1];

        return $"{trimmed} FOR JSON PATH";
    }

    private static void ValidateSelectOnlyQuery(string query)
    {
        if (CommentTokenRegex.IsMatch(query))
            throw new JsonLogicException("The sqlquery rule only allows single SELECT statements and does not allow SQL comments.");

        if (query.Contains(';', StringComparison.Ordinal))
            throw new JsonLogicException("The sqlquery rule only allows single SELECT statements and does not allow semicolons.");

        if (!StartsWithSelectOrWithRegex.IsMatch(query))
            throw new JsonLogicException("The sqlquery rule only allows queries that start with SELECT or WITH.");

        if (ForbiddenKeywordRegex.IsMatch(query))
            throw new JsonLogicException("The sqlquery rule does not allow schema-changing statements (CREATE, ALTER, DROP).");

        if (!Regex.IsMatch(query, @"\bSELECT\b", RegexOptions.IgnoreCase))
            throw new JsonLogicException("The sqlquery rule only allows SELECT statements.");
    }

    private static string BuildConnectionString(string endpointConnectionString)
    {
        SqlConnectionStringBuilder builder;

        if (endpointConnectionString.Contains('=', StringComparison.Ordinal))
        {
            builder = new SqlConnectionStringBuilder(endpointConnectionString);
        }
        else
        {
            builder = new SqlConnectionStringBuilder
            {
                DataSource = endpointConnectionString
            };
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
            throw new InvalidOperationException("Lakehouse SQL endpoint data source is missing.");

        builder.Encrypt = true;
        builder.TrustServerCertificate = false;

        if (builder.ConnectTimeout <= 0)
            builder.ConnectTimeout = 30;

        return builder.ConnectionString;
    }
}

internal class SqlQueryJsonConverter : WeaklyTypedJsonConverter<SqlQueryRule>
{
    public override SqlQueryRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, FabInspectorSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, FabInspectorSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 1)
            throw new JsonException("The sqlquery rule requires at least one parameter: the T-SQL query expression.");

        return new SqlQueryRule(
            parameters[0]!,
            parameters.Length > 1 ? parameters[1]! : Utils.Constants.ContextFabricWorkspace,
            parameters.Length > 2 ? parameters[2]! : Utils.Constants.ContextFabricItem,
            parameters.Length > 3 ? parameters[3] : null,
            parameters.Length > 4 ? parameters[4] : null
        );
    }

    public override void Write(Utf8JsonWriter writer, SqlQueryRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}