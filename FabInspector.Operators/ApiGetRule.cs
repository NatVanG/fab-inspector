using Json.Logic;
using Json.More;
using Microsoft.VisualBasic;
using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Part;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FabInspector.Operators;

/// <summary>
/// Handles the `apiget` operation.
/// Requires <see cref="ContextService.HttpClient"/>
/// Does not currently support continuation tokens or pagination, so is best suited for API calls that return a single page of results.
/// </summary>
[Operator("apiget")]
[JsonConverter(typeof(ApiGetJsonConverter))]
public class ApiGetRule : Json.Logic.Rule
{
    private const string PowerBIApiBaseUrl = "https://api.powerbi.com/v1.0/myorg";
    private const string FabricApiBaseUrl = "https://api.fabric.microsoft.com/v1";

    internal Json.Logic.Rule UrlTemplate { get; }
    internal List<Json.Logic.Rule> UrlParameters { get; }

    internal ApiGetRule(Json.Logic.Rule urlTemplate, List<Json.Logic.Rule> urlParameters)
    {
        UrlTemplate = urlTemplate;
        UrlParameters = urlParameters;
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
        var urlTemplate = UrlTemplate.Apply(data, contextData)?.Stringify();
        
        var parameters = UrlParameters?.Select(p => p.Apply(data, contextData)).ToArray();

        if (string.IsNullOrWhiteSpace(urlTemplate?.ToString()))
            throw new JsonLogicException("The apiget rule requires a non-empty URL template");

        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running pbi-apiget rules.");

        var workspaceId = ContextService.FabricWorkspaceId;

        var itemId = ContextService.FabricItem;

        var credential = ContextService.Credential
            ?? throw new InvalidOperationException("ContextService.Credential is not configured. Ensure authentication has been completed before running daxquery rules.");

        // Resolve the URL template with the provided parameters
        string hostService;
        if (urlTemplate.StartsWith(PowerBIApiBaseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            hostService = "PBI";
        }
        else if (urlTemplate.StartsWith(FabricApiBaseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            hostService = "Fabric";
        }
        else
        {
            throw new JsonLogicException($"Unsupported API host in URL template: {urlTemplate}. Only Power BI and Fabric API endpoints are supported.");
        }

       var resolvedUrl = urlTemplate.Replace(Utils.Constants.ContextFabricWorkspace, workspaceId, StringComparison.InvariantCultureIgnoreCase);
       resolvedUrl = resolvedUrl.Replace(Utils.Constants.ContextFabricItem, itemId, StringComparison.InvariantCultureIgnoreCase);

        //ensure parameters.Length does not exceed the number of placeholders in the urlTemplate
        // placeholders are in the format {paramName}
        var placeholderMatches = Regex.Matches(resolvedUrl, @"\{[a-zA-Z0-9_-]+\}");
        var placeholderCount = placeholderMatches.Count;
        if ((parameters == null && placeholderCount > 0) || (parameters != null && placeholderCount > parameters.Length))
        {
            throw new JsonLogicException($"The apiget rule has more parameters ({parameters.Length}) than placeholders ({placeholderCount}) in the URL template.");
        }

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var placeholder = placeholderMatches[i].Value;
                resolvedUrl = resolvedUrl.Replace(placeholder, parameters[i]?.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        var pbiRequest = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            resolvedUrl,
            credential,
            hostService.Equals("PBI", StringComparison.InvariantCultureIgnoreCase) ? AuthenticationHelper.PowerBIScopes : AuthenticationHelper.FabricScopes).ConfigureAwait(false).GetAwaiter().GetResult();

        var response = ContextService.HttpClient.SendAsync(pbiRequest).ConfigureAwait(false).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            throw new HttpRequestException($"API Get request failed ({response.StatusCode}): {errorContent}");
        }

        var resultJson = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return JsonNode.Parse(resultJson);
    }
}

internal class ApiGetJsonConverter : WeaklyTypedJsonConverter<ApiGetRule>
{
    public override ApiGetRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, FabInspectorSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, FabInspectorSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 1)
            throw new JsonException("The apiget rule requires at least one parameter: the API URL template.");

        return new ApiGetRule(parameters[0], parameters.Length > 1 ? parameters[1..].ToList() : null);
    }

    public override void Write(Utf8JsonWriter writer, ApiGetRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
