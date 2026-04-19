using Json.Logic;
using Json.More;
using FabInspector.Core;
using FabInspector.Core.Part;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FabInspector.Operators;

/// <summary>
/// Handles the `daxquery` operation.
/// Executes a DAX query against a Fabric semantic model using the Fabric REST API
/// and returns the query result as a JSON node.
/// Requires <see cref="ContextService.HttpClient"/>, <see cref="ContextService.FabricWorkspaceId"/>,
/// and <see cref="ContextService.FabricItem"/> to be populated before invocation.
/// </summary>
/// TODO: support local model querying through the PBI Desktop debug port?
[Operator("daxquery")]
[JsonConverter(typeof(DaxQueryJsonConverter))]
public class DaxQueryRule : Json.Logic.Rule
{
    private const string PowerBIApiBaseUrl = "https://api.powerbi.com/v1.0/myorg";

    internal Json.Logic.Rule Query { get; }
    internal Json.Logic.Rule WorspaceId { get; }
    internal Json.Logic.Rule SemanticModelId { get; }
    internal Json.Logic.Rule? IncludeNulls { get; }
    internal Json.Logic.Rule? ImpersonatedUserName { get; }


    internal DaxQueryRule(Json.Logic.Rule query, Json.Logic.Rule workspaceId, Json.Logic.Rule semanticModelId, Json.Logic.Rule? includeNulls, Json.Logic.Rule? impersonatedUserName)
    {
        Query = query;
        WorspaceId = workspaceId;
        SemanticModelId = semanticModelId;
        IncludeNulls = includeNulls;
        ImpersonatedUserName = impersonatedUserName;
    }

    /// <summary>
    /// Applies the rule to the input data by resolving the DAX query string and
    /// posting it to the Fabric ExecuteQueries REST API endpoint.
    /// </summary>
    /// <param name="data">The input data used to resolve the DAX query expression.</param>
    /// <param name="contextData">Optional secondary data context passed to inner operators.</param>
    /// <returns>The JSON result returned by the Fabric ExecuteQueries API.</returns>
    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var queryValue = Query.Apply(data, contextData);
        var strQuery = queryValue?.Stringify();

        var workspaceId = WorspaceId?.Apply(data, contextData)?.Stringify();
        workspaceId = workspaceId!.Equals(Utils.Constants.ContextFabricWorkspace, StringComparison.OrdinalIgnoreCase) ? ContextService.FabricWorkspaceId : workspaceId;
        if (string.IsNullOrWhiteSpace(workspaceId) || !Guid.TryParse(workspaceId, out _))
            throw new InvalidOperationException("WorkspaceId is not configured.");

        var semanticModelId = SemanticModelId?.Apply(data, contextData)?.Stringify();
        semanticModelId = semanticModelId!.Equals(Utils.Constants.ContextFabricItem, StringComparison.OrdinalIgnoreCase) ? ContextService.FabricItem : semanticModelId;
        if (string.IsNullOrWhiteSpace(semanticModelId) || !Guid.TryParse(semanticModelId, out _))
            throw new InvalidOperationException("SemanticModelId is not configured.");

        var includeNullsValue = IncludeNulls?.Apply(data, contextData);
        var impersonatedUserNameValue = ImpersonatedUserName?.Apply(data, contextData);

        var boolIncludeNulls = includeNullsValue?.GetValue<bool>() ?? false;
        var strImpersonatedUserName = impersonatedUserNameValue?.Stringify();

        if (string.IsNullOrWhiteSpace(strQuery?.ToString()))
            throw new JsonLogicException("The daxquery rule requires a non-empty DAX query");

        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running daxquery rules.");

        var tokenProvider = ContextService.TokenProvider
            ?? throw new InvalidOperationException("ContextService.TokenProvider is not configured. Ensure authentication has been completed before running daxquery rules.");

        var url = $"{PowerBIApiBaseUrl}/groups/{Uri.EscapeDataString(workspaceId)}/datasets/{Uri.EscapeDataString(semanticModelId)}/executeQueries";

        ContextService.ReportOperatorProgress("daxquery", $"Starting DAX query execution for workspace '{workspaceId}' and semantic model '{semanticModelId}'.");


        string requestBody;
           
        if (strImpersonatedUserName != null)
        {
            requestBody = JsonSerializer.Serialize(new
            {
                queries = new[] { new { query = strQuery } },
                serializerSettings = new { includeNulls = boolIncludeNulls },
                impersonatedUserName = strImpersonatedUserName, // Optional: specify a user to impersonate for the query execution, or leave empty to use the authenticated user context 
            });
        }
        else
        {
            requestBody = JsonSerializer.Serialize(new
            {
                queries = new[] { new { query = strQuery } },
                serializerSettings = new { includeNulls = boolIncludeNulls },
            });
        }

        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        //var response = httpClient.PostAsync(url, content).GetAwaiter().GetResult();

        // Power BI API call � same HttpClient, different token for Power BI scopes instead of Fabric scopes
        var pbiRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Post,
            url,
            tokenProvider,
            AuthenticationHelper.PowerBIScopes,
            content).ConfigureAwait(false).GetAwaiter().GetResult();

        var response = ContextService.HttpClient.SendAsync(pbiRequest).ConfigureAwait(false).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            ContextService.ReportOperatorProgress("daxquery", $"DAX query execution failed with status {(int)response.StatusCode} {response.StatusCode} after {stopwatch.ElapsedMilliseconds} ms.");
            throw new HttpRequestException($"DAX query execution failed ({response.StatusCode}): {errorContent}");
        }

        var resultJson = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        ContextService.ReportOperatorProgress("daxquery", $"Completed DAX query execution in {stopwatch.ElapsedMilliseconds} ms.");
        return JsonNode.Parse(resultJson);
    }
}

internal class DaxQueryJsonConverter : WeaklyTypedJsonConverter<DaxQueryRule>
{
    public override DaxQueryRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, FabInspectorSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, FabInspectorSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 1)
            throw new JsonException("The daxquery rule requires at least one parameter: the DAX query expression.");

        return new DaxQueryRule(
            parameters[0]!,
            parameters.Length > 1 ? parameters[1]! : Utils.Constants.ContextFabricWorkspace,
            parameters.Length > 2 ? parameters[2]! : Utils.Constants.ContextFabricItem,
            parameters.Length > 3 ? parameters[3] : null,
            parameters.Length > 4 ? parameters[4] : null
        );
    }

    public override void Write(Utf8JsonWriter writer, DaxQueryRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
