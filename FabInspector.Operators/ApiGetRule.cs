using Json.Logic;
using Json.More;
using FabInspector.Core;
using FabInspector.Core.Part;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FabInspector.Operators;

/// <summary>
/// Handles the `apiget` operation.
/// Requires <see cref="ContextService.HttpClient"/>
/// </summary>
[Operator("apiget")]
[JsonConverter(typeof(ApiGetJsonConverter))]
public class ApiGetRule : Json.Logic.Rule
{
    private const string PowerBIApiBaseUrl = "https://api.powerbi.com/v1.0/myorg";
    private const string FabricApiBaseUrl = "https://api.fabric.microsoft.com/v1";
    private const int MaxPaginationPages = 1000;

    internal Json.Logic.Rule UrlTemplate { get; }
    internal List<Json.Logic.Rule>? UrlParameters { get; }

    internal ApiGetRule(Json.Logic.Rule urlTemplate, List<Json.Logic.Rule>? urlParameters)
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
        var stopwatch = Stopwatch.StartNew();
        var urlTemplate = UrlTemplate.Apply(data, contextData)?.Stringify();
        
        var parameters = UrlParameters?.Select(p => p.Apply(data, contextData)).ToArray();

        if (string.IsNullOrWhiteSpace(urlTemplate?.ToString()))
            throw new JsonLogicException("The apiget rule requires a non-empty URL template");

        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running pbi-apiget rules.");

        var workspaceId = ContextService.FabricWorkspaceId;

        var itemId = ContextService.FabricItem;

        var tokenProvider = ContextService.TokenProvider
            ?? throw new InvalidOperationException("ContextService.TokenProvider is not configured. Ensure authentication has been completed before running apiget rules.");

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
        if (parameters == null && placeholderCount > 0)
        {
            throw new JsonLogicException($"The apiget rule requires {placeholderCount} placeholder parameter(s) but none were provided.");
        }
        if (parameters != null && placeholderCount > parameters.Length)
        {
            throw new JsonLogicException($"The apiget rule has more placeholders ({placeholderCount}) than parameters ({parameters.Length}) in the URL template.");
        }

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var placeholder = placeholderMatches[i].Value;
                resolvedUrl = resolvedUrl.Replace(placeholder, parameters[i]?.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        var progressTarget = GetProgressTarget(resolvedUrl);
        ContextService.ReportOperatorProgress("apiget", $"Starting GET request to {hostService} endpoint '{progressTarget}'.");

        var visitedUrls = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var allValueItems = new JsonArray();
        JsonObject? firstPageObject = null;
        JsonNode? lastPageNode = null;

        var nextUrl = resolvedUrl;
        var pageNumber = 0;

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            pageNumber++;
            if (pageNumber > MaxPaginationPages)
            {
                throw new JsonLogicException($"The apiget rule exceeded the maximum pagination page limit ({MaxPaginationPages}).");
            }

            if (!visitedUrls.Add(nextUrl))
            {
                throw new JsonLogicException($"The apiget rule detected a pagination loop at URL '{nextUrl}'.");
            }

            if (pageNumber > 1)
            {
                ContextService.ReportOperatorProgress("apiget", $"Fetching continuation page {pageNumber} for '{progressTarget}'.");
            }

            var request = AuthenticationHelper.CreateAuthenticatedRequestAsync(
                HttpMethod.Get,
                nextUrl,
                tokenProvider,
                hostService.Equals("PBI", StringComparison.InvariantCultureIgnoreCase) ? AuthenticationHelper.PowerBIScopes : AuthenticationHelper.FabricScopes).ConfigureAwait(false).GetAwaiter().GetResult();

            var response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                ContextService.ReportOperatorProgress("apiget", $"GET request to '{progressTarget}' failed with status {(int)response.StatusCode} {response.StatusCode} after {stopwatch.ElapsedMilliseconds} ms.");
                throw new HttpRequestException($"API Get request failed ({response.StatusCode}): {errorContent}");
            }

            var resultJson = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var pageNode = JsonNode.Parse(resultJson);
            lastPageNode = pageNode;

            if (pageNode is JsonObject pageObject)
            {
                firstPageObject ??= pageObject.DeepClone().AsObject();

                if (pageObject["value"] is JsonArray valueArray)
                {
                    foreach (var item in valueArray)
                    {
                        allValueItems.Add(item?.DeepClone());
                    }
                }
            }

            var continuation = ResolveContinuationUrl(response, pageNode, nextUrl);
            nextUrl = continuation;
        }

        ContextService.ReportOperatorProgress("apiget", $"Completed GET request to '{progressTarget}' in {stopwatch.ElapsedMilliseconds} ms.");

        if (firstPageObject != null && allValueItems.Count > 0)
        {
            firstPageObject["value"] = allValueItems;
            firstPageObject.Remove("continuationToken");
            firstPageObject.Remove("continuationUri");
            firstPageObject.Remove("@odata.nextLink");
            return firstPageObject;
        }

        return lastPageNode;
    }

    private static string GetProgressTarget(string resolvedUrl)
    {
        return Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path)
            : resolvedUrl;
    }

    private static string? ResolveContinuationUrl(HttpResponseMessage response, JsonNode? pageNode, string currentUrl)
    {
        if (TryGetHeaderValue(response, "x-ms-continuationuri", out var continuationUriFromHeader) ||
            TryGetHeaderValue(response, "continuationuri", out continuationUriFromHeader))
        {
            return continuationUriFromHeader;
        }

        if (TryGetHeaderValue(response, "x-ms-continuationtoken", out var continuationTokenFromHeader) ||
            TryGetHeaderValue(response, "continuationtoken", out continuationTokenFromHeader))
        {
            return UpsertQueryParameter(currentUrl, "continuationToken", continuationTokenFromHeader);
        }

        if (pageNode is not JsonObject pageObject)
        {
            return null;
        }

        var continuationUri = pageObject["continuationUri"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(continuationUri))
        {
            return continuationUri;
        }

        var odataNextLink = pageObject["@odata.nextLink"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(odataNextLink))
        {
            return odataNextLink;
        }

        var continuationToken = pageObject["continuationToken"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            return UpsertQueryParameter(currentUrl, "continuationToken", continuationToken);
        }

        return null;
    }

    private static bool TryGetHeaderValue(HttpResponseMessage response, string headerName, out string value)
    {
        value = string.Empty;
        if (!response.Headers.TryGetValues(headerName, out var values))
        {
            return false;
        }

        value = values.FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string UpsertQueryParameter(string url, string parameterName, string parameterValue)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{url}{separator}{Uri.EscapeDataString(parameterName)}={Uri.EscapeDataString(parameterValue)}";
        }

        var queryParameters = new List<string>();
        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var equalsIndex = pair.IndexOf('=');
                var key = equalsIndex >= 0 ? pair[..equalsIndex] : pair;
                if (key.Equals(parameterName, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                queryParameters.Add(pair);
            }
        }

        queryParameters.Add($"{Uri.EscapeDataString(parameterName)}={Uri.EscapeDataString(parameterValue)}");

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", queryParameters)
        };

        return builder.Uri.ToString();
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

        return new ApiGetRule(parameters[0]!, parameters.Length > 1 ? parameters[1..].ToList() : null);
    }

    public override void Write(Utf8JsonWriter writer, ApiGetRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
