using Json.Logic;
using Json.More;
using FabInspector.Core;
using FabInspector.Core.Inspection;
using FabInspector.Core.Part;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FabInspector.Operators;

/// <summary>
/// Handles the `dfsget` operation.
/// Requires <see cref="FabInspector.Core.Inspection.InspectionContext.HttpClient"/>.
/// Expects an HTTPS OneLake DFS absolute URL as the first parameter.
/// </summary>
[Operator("dfsget")]
[JsonConverter(typeof(DfsGetJsonConverter))]
public class DfsGetRule : Json.Logic.Rule
{
    private const string OneLakeDfsHostSuffix = ".dfs.fabric.microsoft.com";

    internal Json.Logic.Rule UrlTemplate { get; }
    internal List<Json.Logic.Rule>? UrlParameters { get; }

    internal DfsGetRule(Json.Logic.Rule urlTemplate, List<Json.Logic.Rule>? urlParameters)
    {
        UrlTemplate = urlTemplate;
        UrlParameters = urlParameters;
    }

    /// <summary>
    /// Applies the rule by resolving the DFS URL and issuing an authenticated HTTP GET to OneLake DFS.
    /// </summary>
    /// <param name="data">The input data used to resolve URL expressions.</param>
    /// <param name="contextData">Optional secondary data context passed to inner operators.</param>
    /// <returns>The response content as parsed JSON, or a JSON string node when response is non-JSON.</returns>
    public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var urlTemplate = UrlTemplate.Apply(data, contextData)?.Stringify();
        var parameters = UrlParameters?.Select(p => p.Apply(data, contextData)).ToArray();

        if (string.IsNullOrWhiteSpace(urlTemplate))
            throw new JsonLogicException("The dfsget rule requires a non-empty DFS URL template.");

        if (!Uri.TryCreate(urlTemplate, UriKind.Absolute, out var templateUri) ||
            !templateUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.InvariantCultureIgnoreCase) ||
            !templateUri.Host.EndsWith(OneLakeDfsHostSuffix, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new JsonLogicException($"The dfsget rule requires an HTTPS OneLake DFS absolute URL. Received: {urlTemplate}");
        }

        var ctx = InspectionContextHolder.Require("dfsget");
        var httpClient = ctx.HttpClient;

        var workspaceId = ctx.FabricWorkspaceId;
        var itemId = ctx.FabricItem;

        var tokenProvider = ctx.TokenProvider;

        var resolvedUrl = urlTemplate.Replace(Utils.Constants.ContextFabricWorkspace, workspaceId, StringComparison.InvariantCultureIgnoreCase);
        resolvedUrl = resolvedUrl.Replace(Utils.Constants.ContextFabricItem, itemId, StringComparison.InvariantCultureIgnoreCase);

        var placeholderMatches = Regex.Matches(resolvedUrl, @"\{[a-zA-Z0-9_-]+\}");
        var placeholderCount = placeholderMatches.Count;
        var parameterCount = parameters?.Length ?? 0;
        if (placeholderCount != parameterCount)
        {
            throw new JsonLogicException($"The dfsget rule requires placeholder count ({placeholderCount}) to match provided parameter count ({parameterCount}) in the URL template.");
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
        InspectionContextHolder.ReportOperatorProgress("dfsget", $"Starting GET request to OneLake DFS endpoint '{progressTarget}'.");

        var request = AuthenticationHelper.CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            resolvedUrl,
            tokenProvider,
            AuthenticationHelper.OneLakeDfsScopes).ConfigureAwait(false).GetAwaiter().GetResult();

        var response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            InspectionContextHolder.ReportOperatorProgress("dfsget", $"GET request to '{progressTarget}' failed with status {(int)response.StatusCode} {response.StatusCode} after {stopwatch.ElapsedMilliseconds} ms.");
            throw new HttpRequestException($"DFS Get request failed ({response.StatusCode}): {errorContent}");
        }

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        InspectionContextHolder.ReportOperatorProgress("dfsget", $"Completed GET request to '{progressTarget}' in {stopwatch.ElapsedMilliseconds} ms.");
        return TryParseJsonOrString(responseContent);
    }

    private static JsonNode? TryParseJsonOrString(string responseContent)
    {
        try
        {
            return JsonNode.Parse(responseContent);
        }
        catch (JsonException)
        {
            return JsonValue.Create(responseContent);
        }
    }

    private static string GetProgressTarget(string resolvedUrl)
    {
        return Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path)
            : resolvedUrl;
    }
}

internal class DfsGetJsonConverter : WeaklyTypedJsonConverter<DfsGetRule>
{
    public override DfsGetRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parameters = reader.TokenType == JsonTokenType.StartArray
            ? options.ReadArray(ref reader, FabInspectorSerializerContext.Default.Rule)
            : new[] { options.Read(ref reader, FabInspectorSerializerContext.Default.Rule)! };

        if (parameters == null || parameters.Length < 1)
            throw new JsonException("The dfsget rule requires at least one parameter: the DFS absolute URL template.");

        return new DfsGetRule(parameters[0], parameters.Length > 1 ? parameters[1..].ToList() : null);
    }

    public override void Write(Utf8JsonWriter writer, DfsGetRule value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}