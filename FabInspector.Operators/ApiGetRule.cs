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
    private const string ContinuationTokenQueryParameter = "continuationToken";

    private static readonly string[] ContinuationUriHeaders = ["x-ms-continuationuri", "continuationuri"];
    private static readonly string[] ContinuationTokenHeaders = ["x-ms-continuationtoken", "continuationtoken"];
    private static readonly string[] ContinuationUriBodyProperties = ["continuationUri", "@odata.nextLink"];
    private static readonly string[] ContinuationTokenBodyProperties = ["continuationToken"];

    internal Json.Logic.Rule UrlTemplate { get; }
    internal List<Json.Logic.Rule>? UrlParameters { get; }

    internal ApiGetRule(Json.Logic.Rule urlTemplate, List<Json.Logic.Rule>? urlParameters)
    {
        UrlTemplate = urlTemplate;
        UrlParameters = urlParameters;
    }
    
    /// <summary>
    /// Applies the rule to the input data by resolving a templated API URL and
    /// issuing authenticated GET requests, including continuation-page traversal when provided by the API.
    /// </summary>
    /// <param name="data">The input data used to resolve the URL template and optional parameters.</param>
    /// <param name="contextData">Optional secondary data context passed to inner operators.</param>
    /// <returns>The JSON result returned by the resolved Power BI or Fabric API endpoint.</returns>
    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var urlTemplate = UrlTemplate.Apply(data, contextData)?.Stringify();
        var parameters = UrlParameters?.Select(p => p.Apply(data, contextData)).ToArray();

        if (string.IsNullOrWhiteSpace(urlTemplate))
            throw new JsonLogicException("The apiget rule requires a non-empty URL template");

        var httpClient = ContextService.HttpClient
            ?? throw new InvalidOperationException("ContextService.HttpClient is not configured. Ensure authentication has been completed before running pbi-apiget rules.");
        var tokenProvider = ContextService.TokenProvider
            ?? throw new InvalidOperationException("ContextService.TokenProvider is not configured. Ensure authentication has been completed before running apiget rules.");

        var hostService = ResolveHostService(urlTemplate);
        var resolvedUrl = ResolveUrl(urlTemplate, parameters, ContextService.FabricWorkspaceId, ContextService.FabricItem);

        var progressTarget = GetProgressTarget(resolvedUrl);
        ContextService.ReportOperatorProgress("apiget", $"Starting GET request to {hostService} endpoint '{progressTarget}'.");

        var pageAccumulator = ExecutePagedGet(
            httpClient,
            tokenProvider,
            hostService,
            resolvedUrl,
            progressTarget,
            stopwatch);

        ContextService.ReportOperatorProgress("apiget", $"Completed GET request to '{progressTarget}' in {stopwatch.ElapsedMilliseconds} ms.");

        return pageAccumulator.BuildResult();
    }

    private static string ResolveHostService(string urlTemplate)
    {
        if (urlTemplate.StartsWith(PowerBIApiBaseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            return "PBI";
        }

        if (urlTemplate.StartsWith(FabricApiBaseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            return "Fabric";
        }

        throw new JsonLogicException($"Unsupported API host in URL template: {urlTemplate}. Only Power BI and Fabric API endpoints are supported.");
    }

    private static string ResolveUrl(string urlTemplate, JsonNode?[]? parameters, string? workspaceId, string? itemId)
    {
        var resolvedUrl = urlTemplate.Replace(Utils.Constants.ContextFabricWorkspace, workspaceId, StringComparison.InvariantCultureIgnoreCase);
        resolvedUrl = resolvedUrl.Replace(Utils.Constants.ContextFabricItem, itemId, StringComparison.InvariantCultureIgnoreCase);

        var placeholderMatches = Regex.Matches(resolvedUrl, @"\{[a-zA-Z0-9_-]+\}");
        ValidatePlaceholderAndParameterCounts(placeholderMatches.Count, parameters?.Length ?? 0);

        if (parameters == null)
        {
            return resolvedUrl;
        }

        for (int i = 0; i < placeholderMatches.Count; i++)
        {
            var placeholder = placeholderMatches[i].Value;
            resolvedUrl = resolvedUrl.Replace(placeholder, parameters[i]?.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        return resolvedUrl;
    }

    private static void ValidatePlaceholderAndParameterCounts(int placeholderCount, int parameterCount)
    {
        if (placeholderCount == 0 && parameterCount == 0)
        {
            return;
        }

        if (placeholderCount > parameterCount)
        {
            throw new JsonLogicException($"The apiget rule has more placeholders ({placeholderCount}) than parameters ({parameterCount}) in the URL template.");
        }

        if (parameterCount > placeholderCount)
        {
            throw new JsonLogicException($"The apiget rule has more parameters ({parameterCount}) than placeholders ({placeholderCount}) in the URL template.");
        }
    }

    private static PageAccumulator ExecutePagedGet(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        string hostService,
        string resolvedUrl,
        string progressTarget,
        Stopwatch stopwatch)
    {
        var visitedUrls = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var pageAccumulator = new PageAccumulator();

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
                hostService.Equals("PBI", StringComparison.InvariantCultureIgnoreCase)
                    ? AuthenticationHelper.PowerBIScopes
                    : AuthenticationHelper.FabricScopes).ConfigureAwait(false).GetAwaiter().GetResult();

            var response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                ContextService.ReportOperatorProgress("apiget", $"GET request to '{progressTarget}' failed with status {(int)response.StatusCode} {response.StatusCode} after {stopwatch.ElapsedMilliseconds} ms.");
                throw new HttpRequestException($"API Get request failed ({response.StatusCode}): {errorContent}");
            }

            var resultJson = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var pageNode = JsonNode.Parse(resultJson);
            pageAccumulator.AddPage(pageNode);

            nextUrl = ResolveContinuationUrl(response, pageNode, nextUrl);
        }

        return pageAccumulator;
    }

    private sealed class PageAccumulator
    {
        private readonly JsonArray _allValueItems = [];
        private JsonObject? _firstPageObject;
        private JsonNode? _lastPageNode;

        public void AddPage(JsonNode? pageNode)
        {
            _lastPageNode = pageNode;

            if (pageNode is not JsonObject pageObject)
            {
                return;
            }

            _firstPageObject ??= pageObject.DeepClone().AsObject();

            if (pageObject["value"] is not JsonArray valueArray)
            {
                return;
            }

            foreach (var item in valueArray)
            {
                _allValueItems.Add(item?.DeepClone());
            }
        }

        public JsonNode? BuildResult()
        {
            if (_firstPageObject != null && _allValueItems.Count > 0)
            {
                _firstPageObject["value"] = _allValueItems;
                _firstPageObject.Remove("continuationToken");
                _firstPageObject.Remove("continuationUri");
                _firstPageObject.Remove("@odata.nextLink");
                return _firstPageObject;
            }

            return _lastPageNode;
        }
    }

    private static string GetProgressTarget(string resolvedUrl)
    {
        return Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path)
            : resolvedUrl;
    }

    private static string? ResolveContinuationUrl(HttpResponseMessage response, JsonNode? pageNode, string currentUrl)
    {
        if (TryGetFirstHeaderValue(response, ContinuationUriHeaders, out var continuationUriFromHeader))
        {
            return continuationUriFromHeader;
        }

        if (TryGetFirstBodyPropertyValue(pageNode, ContinuationUriBodyProperties, out var continuationUriFromBody))
        {
            return continuationUriFromBody;
        }

        if (TryGetFirstHeaderValue(response, ContinuationTokenHeaders, out var continuationTokenFromHeader))
        {
            return UpsertQueryParameter(currentUrl, ContinuationTokenQueryParameter, continuationTokenFromHeader);
        }

        if (TryGetFirstBodyPropertyValue(pageNode, ContinuationTokenBodyProperties, out var continuationTokenFromBody))
        {
            return UpsertQueryParameter(currentUrl, ContinuationTokenQueryParameter, continuationTokenFromBody);
        }

        return null;
    }

    private static bool TryGetFirstBodyPropertyValue(JsonNode? pageNode, IEnumerable<string> propertyNames, out string value)
    {
        value = string.Empty;
        if (pageNode is not JsonObject pageObject)
        {
            return false;
        }

        foreach (var propertyName in propertyNames)
        {
            var propertyValue = pageObject[propertyName]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(propertyValue))
            {
                value = propertyValue;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFirstHeaderValue(HttpResponseMessage response, IEnumerable<string> headerNames, out string value)
    {
        value = string.Empty;
        foreach (var headerName in headerNames)
        {
            if (TryGetHeaderValue(response, headerName, out value))
            {
                return true;
            }
        }

        return false;
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
