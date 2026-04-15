using Json.Logic;
using Json.More;
using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Part;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FabInspector.Operators;

/// <summary>
/// Handles the <c>scannerapi</c> operation.
/// Initiates a Power BI metadata scan via the Admin WorkspaceInfo Scanner API, polls until
/// the scan succeeds, and returns the final scan result as a JSON node.
/// <para>
/// API flow:
/// <list type="number">
///   <item><description>POST <c>https://api.powerbi.com/v1.0/myorg/admin/workspaces/getInfo</c> with the workspace IDs and optional query flags.</description></item>
///   <item><description>Poll <c>GET …/scanStatus/{scanId}</c> until status is <c>Succeeded</c> or <c>Failed</c>.</description></item>
///   <item><description>GET <c>…/scanResult/{scanId}</c> and return the result.</description></item>
/// </list>
/// </para>
/// Parameters (positional array in rule JSON):
/// <list type="number">
///   <item><description><b>workspaceIds</b> (required) – a single workspace-ID string, a JSON array of workspace-ID strings, or a comma-separated list. Defaults to <see cref="ContextService.FabricWorkspaceId"/> when omitted or empty.</description></item>
///   <item><description><b>lineage</b> (optional bool) – return lineage info.</description></item>
///   <item><description><b>datasourceDetails</b> (optional bool) – return data source details.</description></item>
///   <item><description><b>datasetSchema</b> (optional bool) – return dataset schema (tables, columns, measures).</description></item>
///   <item><description><b>datasetExpressions</b> (optional bool) – return dataset expressions (DAX / Mashup).</description></item>
///   <item><description><b>getArtifactUsers</b> (optional bool) – return user details for Power BI items.</description></item>
/// </list>
/// Requires <see cref="ContextService.HttpClient"/> and <see cref="ContextService.Credential"/> to be configured.
/// </summary>
[Operator("scannerapi")]
[JsonConverter(typeof(ScannerApiJsonConverter))]
public class ScannerApiRule : Json.Logic.Rule
{
    private const string PostWorkspaceInfoUrl = "https://api.powerbi.com/v1.0/myorg/admin/workspaces/getInfo";
    private const string ScanStatusUrlTemplate = "https://api.powerbi.com/v1.0/myorg/admin/workspaces/scanStatus/{0}";
    private const string ScanResultUrlTemplate = "https://api.powerbi.com/v1.0/myorg/admin/workspaces/scanResult/{0}";

    private const int MaxPollAttempts = 60;
    private const int PollIntervalMilliseconds = 5_000;

    internal Json.Logic.Rule WorkspaceIds { get; }
    internal Json.Logic.Rule? Lineage { get; }
    internal Json.Logic.Rule? DatasourceDetails { get; }
    internal Json.Logic.Rule? DatasetSchema { get; }
    internal Json.Logic.Rule? DatasetExpressions { get; }
    internal Json.Logic.Rule? GetArtifactUsers { get; }

    internal ScannerApiRule(
        Json.Logic.Rule workspaceIds,
        Json.Logic.Rule? lineage,
        Json.Logic.Rule? datasourceDetails,
        Json.Logic.Rule? datasetSchema,
        Json.Logic.Rule? datasetExpressions,
        Json.Logic.Rule? getArtifactUsers)
    {
        WorkspaceIds = workspaceIds;
        Lineage = lineage;
        DatasourceDetails = datasourceDetails;
        DatasetSchema = datasetSchema;
        DatasetExpressions = datasetExpressions;
        GetArtifactUsers = getArtifactUsers;
    }

    /// <summary>
    /// Executes the three-step Scanner API call (POST → poll status → GET result)
    /// and returns the workspace info scan result as a <see cref="JsonNode"/>.
    /// </summary>
    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running scannerapi rules.");

        var credential = ContextService.Credential
            ?? throw new InvalidOperationException("ContextService.Credential is not configured. Ensure authentication has been completed before running scannerapi rules.");

        // --- Resolve workspace IDs ---
        var workspaceIdsNode = WorkspaceIds.Apply(data, contextData);
        var workspaceIds = ResolveWorkspaceIds(workspaceIdsNode);

        if (workspaceIds.Length == 0)
            throw new JsonLogicException("The scannerapi rule requires at least one workspace ID.");

        // --- Build query string ---
        var queryParams = new List<string>();
        AppendBoolParam(queryParams, "lineage", Lineage, data, contextData);
        AppendBoolParam(queryParams, "datasourceDetails", DatasourceDetails, data, contextData);
        AppendBoolParam(queryParams, "datasetSchema", DatasetSchema, data, contextData);
        AppendBoolParam(queryParams, "datasetExpressions", DatasetExpressions, data, contextData);
        AppendBoolParam(queryParams, "getArtifactUsers", GetArtifactUsers, data, contextData);

        var postUrl = queryParams.Count > 0
            ? $"{PostWorkspaceInfoUrl}?{string.Join("&", queryParams)}"
            : PostWorkspaceInfoUrl;

        // --- Step 1: POST to initiate the scan ---
        var requestBody = JsonSerializer.Serialize(new { workspaces = workspaceIds });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var postRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Post,
            postUrl,
            credential,
            AuthenticationHelper.PowerBIScopes,
            content).ConfigureAwait(false).GetAwaiter().GetResult();

        var postResponse = httpClient.SendAsync(postRequest).ConfigureAwait(false).GetAwaiter().GetResult();

        if (!postResponse.IsSuccessStatusCode)
        {
            var errorContent = postResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            throw new HttpRequestException($"Scanner API POST failed ({postResponse.StatusCode}): {errorContent}");
        }

        var scanResponseJson = postResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        var scanResponse = JsonNode.Parse(scanResponseJson);
        var scanId = scanResponse?["id"]?.GetValue<string>()
            ?? throw new HttpRequestException("Scanner API did not return a scan ID in the response.");

        // --- Step 2: Poll for scan status ---
        var statusUrl = string.Format(ScanStatusUrlTemplate, Uri.EscapeDataString(scanId));
        bool succeeded = false;

        for (int attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(PollIntervalMilliseconds);

            var statusRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
                HttpMethod.Get,
                statusUrl,
                credential,
                AuthenticationHelper.PowerBIScopes).ConfigureAwait(false).GetAwaiter().GetResult();

            var statusResponse = httpClient.SendAsync(statusRequest).ConfigureAwait(false).GetAwaiter().GetResult();

            if (!statusResponse.IsSuccessStatusCode)
            {
                var errorContent = statusResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                throw new HttpRequestException($"Scanner API scan-status check failed ({statusResponse.StatusCode}): {errorContent}");
            }

            var statusJson = statusResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var statusNode = JsonNode.Parse(statusJson);
            var status = statusNode?["status"]?.GetValue<string>();

            if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                succeeded = true;
                break;
            }

            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = statusNode?["error"]?["message"]?.GetValue<string>() ?? "Unknown error";
                throw new HttpRequestException($"Scanner API scan failed: {errorMessage}");
            }
        }

        if (!succeeded)
            throw new TimeoutException($"Scanner API scan '{scanId}' did not complete after {MaxPollAttempts} polling attempts ({MaxPollAttempts * PollIntervalMilliseconds / 1000} seconds).");

        // --- Step 3: Retrieve scan result ---
        var resultUrl = string.Format(ScanResultUrlTemplate, Uri.EscapeDataString(scanId));

        var resultRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            resultUrl,
            credential,
            AuthenticationHelper.PowerBIScopes).ConfigureAwait(false).GetAwaiter().GetResult();

        var resultResponse = httpClient.SendAsync(resultRequest).ConfigureAwait(false).GetAwaiter().GetResult();

        if (!resultResponse.IsSuccessStatusCode)
        {
            var errorContent = resultResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            throw new HttpRequestException($"Scanner API scan-result retrieval failed ({resultResponse.StatusCode}): {errorContent}");
        }

        var resultJson = resultResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return JsonNode.Parse(resultJson);
    }

    private static string[] ResolveWorkspaceIds(JsonNode? node)
    {
        if (node is JsonArray arr)
        {
            return arr
                .Select(n => n?.GetValue<string>() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
        }

        var str = node?.Stringify();
        if (string.IsNullOrWhiteSpace(str))
        {
            // Fall back to the current workspace context
            var contextId = ContextService.FabricWorkspaceId;
            return string.IsNullOrWhiteSpace(contextId) ? [] : [contextId];
        }

        str = str.Replace(Utils.Constants.ContextFabricWorkspace, ContextService.FabricWorkspaceId ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Support a single ID or a comma-separated list
        return str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AppendBoolParam(
        List<string> queryParams,
        string name,
        Json.Logic.Rule? rule,
        JsonNode? data,
        JsonNode? contextData)
    {
        if (rule == null) return;

        var value = rule.Apply(data, contextData);
        if (value == null) return;

        try
        {
            var boolValue = value.GetValue<bool>();
            queryParams.Add($"{name}={boolValue.ToString().ToLowerInvariant()}");
        }
        catch (InvalidOperationException)
        {
            // Value is not a boolean; skip this parameter
        }
    }
}

internal class ScannerApiJsonConverter : WeaklyTypedJsonConverter<ScannerApiRule>
{
    public override ScannerApiRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, FabInspectorSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, FabInspectorSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 1)
            throw new JsonException("The scannerapi rule requires at least one parameter: the workspace ID(s) to scan.");

        return new ScannerApiRule(
            workspaceIds: parameters[0],
            lineage: parameters.Length > 1 ? parameters[1] : null,
            datasourceDetails: parameters.Length > 2 ? parameters[2] : null,
            datasetSchema: parameters.Length > 3 ? parameters[3] : null,
            datasetExpressions: parameters.Length > 4 ? parameters[4] : null,
            getArtifactUsers: parameters.Length > 5 ? parameters[5] : null);
    }

    public override void Write(Utf8JsonWriter writer, ScannerApiRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
