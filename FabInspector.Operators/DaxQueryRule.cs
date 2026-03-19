using Json.Logic;
using Json.More;
using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Part;
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

    internal Json.Logic.Rule Input { get; }

    internal DaxQueryRule(Json.Logic.Rule input)
    {
        Input = input;
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
        var queryValue = Input.Apply(data, contextData);
        var query = queryValue?.Stringify();

        if (string.IsNullOrWhiteSpace(query))
            throw new JsonLogicException("The daxquery rule requires a non-empty DAX query string.");

        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running daxquery rules.");

        var workspaceId = ContextService.FabricWorkspaceId
            ?? throw new InvalidOperationException("ContextService.FabricWorkspaceId is not configured.");

        var semanticModelId = ContextService.FabricItem
            ?? throw new InvalidOperationException("ContextService.FabricItem is not configured.");

        var credential = ContextService.Credential
            ?? throw new InvalidOperationException("ContextService.Credential is not configured. Ensure authentication has been completed before running daxquery rules.");

        var url = $"{PowerBIApiBaseUrl}/groups/{Uri.EscapeDataString(workspaceId)}/datasets/{Uri.EscapeDataString(semanticModelId)}/executeQueries";

        var requestBody = JsonSerializer.Serialize(new
        {
            queries = new[] { new { query } },
            //impersonatedUserName = string.Empty, // Optional: specify a user to impersonate for the query execution, or leave empty to use the authenticated user context 
            serializerSettings = new { includeNulls = false }
        });

        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        //var response = httpClient.PostAsync(url, content).GetAwaiter().GetResult();

        // Power BI API call — same HttpClient, different token for Power BI scopes instead of Fabric scopes
        var pbiRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Post,
            url,
            credential,
            AuthenticationHelper.PowerBIScopes,
            content).GetAwaiter().GetResult();

        var response = ContextService.HttpClient.SendAsync(pbiRequest).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new HttpRequestException($"DAX query execution failed ({response.StatusCode}): {errorContent}");
        }

        var resultJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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

        if (parameters == null || parameters.Length != 1)
            throw new JsonException("The daxquery rule requires exactly one parameter: the DAX query expression.");

        return new DaxQueryRule(parameters[0]);
    }

    public override void Write(Utf8JsonWriter writer, DaxQueryRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
