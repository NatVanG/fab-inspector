using Azure.Core;
using FabInspector.Operators;
using NUnit.Framework;
using FabInspector.Core;
using FabInspector.Core.Inspection;
using FabInspector.Core.Output;
using Ric.Operators;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace FabInspector.Tests;

[TestFixture]
[NonParallelizable]
public class OperatorProgressReportingTests
{
    private static readonly JsonLogicOperatorRegistry[] Registries =
    [
        new JsonLogicOperatorRegistry(
            new RicSerializerContext(),
            [
                new CountOperator(),
                new DrillVariableOperator(),
                new FileSizeOperator(),
                new FileTextSearchCountOperator(),
                new IsNullOrEmptyOperator(),
                new PartInfoOperator(),
                new PartOperator(),
                new PathOperator(),
                new QueryOperator(),
                new SetDifferenceOperator(),
                new SetEqualOperator(),
                new SetIntersectionOperator(),
                new SetIntersectOperator(),
                new SetSymmetricDifferenceOperator(),
                new SetUnionOperator(),
                new StringContainsOperator(),
                new ToRecordOperator(),
                new ToStringOperator(),
                new FromYamlFileOperator(),
                new RectangleOverlapOperator()
            ]),
        new JsonLogicOperatorRegistry(
            new FabInspectorSerializerContext(),
            [
                new DaxQueryOperator(),
                new ApiGetOperator(),
                new ScannerApiOperator()
            ])
    ];

    private readonly List<string> _tempPaths = new();

    // Per-test fixture state populated by individual tests and consumed by RunInspection
    // when constructing the ambient InspectionContext. Replaces the legacy ContextService.*
    // static slots that were deleted in Phase 5 of the DI refactor.
    private HttpClient? _httpClient;
    private ITokenProvider? _tokenProvider;
    private string? _fabricWorkspaceId;
    private string? _fabricItem;

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        _tokenProvider = null;
        _fabricWorkspaceId = null;
        _fabricItem = null;

        foreach (var tempPath in _tempPaths)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        _tempPaths.Clear();
    }

    [Test]
    public void ApiGet_EmitsProgressMessagesThroughInspector()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[1]}"));
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());

        var result = RunInspection(
            "api-get-progress.json",
            "{\"apiget\":[\"https://api.fabric.microsoft.com/v1/workspaces\"]}",
            JsonNode.Parse("{\"value\":[1]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(result.Messages, Has.Some.Contains("Operator \"apiget\" - Starting GET request"));
        Assert.That(result.Messages, Has.Some.Contains("Operator \"apiget\" - Completed GET request"));
    }

    [Test]
    public void ApiGet_PaginatesUsingFabricContinuationToken()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[{\"id\":1}],\"continuationToken\":\"token-page-2\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[{\"id\":2}]}"));
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());

        var result = RunInspection(
            "api-get-pagination-fabric.json",
            "{\"apiget\":[\"https://api.fabric.microsoft.com/v1/workspaces\"]}",
            JsonNode.Parse("{\"value\":[{\"id\":1},{\"id\":2}]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(result.Messages, Has.Some.Contains("continuation page 2"));
    }

    [Test]
    public void ApiGet_PaginatesUsingPowerBiODataNextLink()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[{\"id\":\"A\"}],\"@odata.nextLink\":\"https://api.powerbi.com/v1.0/myorg/groups?$skiptoken=abc123\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[{\"id\":\"B\"}]}"));
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());

        var result = RunInspection(
            "api-get-pagination-pbi.json",
            "{\"apiget\":[\"https://api.powerbi.com/v1.0/myorg/groups\"]}",
            JsonNode.Parse("{\"value\":[{\"id\":\"A\"},{\"id\":\"B\"}]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(result.Messages, Has.Some.Contains("continuation page 2"));
    }

    [Test]
    public void ApiGet_PrefersContinuationUriOverContinuationToken()
    {
        var firstResponse = CreateJsonResponse(
            HttpStatusCode.OK,
            "{\"value\":[{\"id\":1}],\"continuationUri\":\"https://api.fabric.microsoft.com/v1/workspaces?continuationToken=uri-token\"}");
        firstResponse.Headers.Add("x-ms-continuationtoken", "header-token");

        _httpClient = CreateHttpClient(
            out var handler,
            firstResponse,
            CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[{\"id\":2}]}")
        );
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());

        var result = RunInspection(
            "api-get-continuation-uri-precedence.json",
            "{\"apiget\":[\"https://api.fabric.microsoft.com/v1/workspaces\"]}",
            JsonNode.Parse("{\"value\":[{\"id\":1},{\"id\":2}]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(handler.RequestedUris.Count, Is.EqualTo(2));
        Assert.That(handler.RequestedUris[1], Is.EqualTo("https://api.fabric.microsoft.com/v1/workspaces?continuationToken=uri-token"));
    }

    [Test]
    public void ApiGet_ReturnsLastPageWhenValueArrayIsAbsent()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"continuationToken\":\"token-page-2\",\"meta\":\"first\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"meta\":\"second\"}"));
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());

        var result = RunInspection(
            "api-get-no-value-array-pagination.json",
            "{\"apiget\":[\"https://api.fabric.microsoft.com/v1/workspaces\"]}",
            JsonNode.Parse("{\"meta\":\"second\"}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
    }

    [Test]
    public void DaxQuery_EmitsProgressMessagesThroughInspector()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"results\":[{\"tables\":[{\"rows\":[{\"Value\":1}]}]}]}"));
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());
        _fabricWorkspaceId = "11111111-1111-1111-1111-111111111111";
        _fabricItem = "22222222-2222-2222-2222-222222222222";

        var result = RunInspection(
            "dax-query-progress.json",
            "{\"daxquery\":[\"EVALUATE ROW(\\\"Value\\\", 1)\"]}",
            JsonNode.Parse("{\"results\":[{\"tables\":[{\"rows\":[{\"Value\":1}]}]}]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(result.Messages, Has.Some.Contains("Operator \"daxquery\" - Starting DAX query execution"));
        Assert.That(result.Messages, Has.Some.Contains("Operator \"daxquery\" - Completed DAX query execution"));
    }

    [Test]
    public void ScannerApi_EmitsBoundedPollingProgressThroughInspector()
    {
        _httpClient = CreateHttpClient(
            CreateJsonResponse(HttpStatusCode.OK, "{\"id\":\"scan-123\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Running\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Running\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Running\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Running\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Running\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"status\":\"Succeeded\"}"),
            CreateJsonResponse(HttpStatusCode.OK, "{\"workspaces\":[{\"id\":\"ws-1\"}]}")
        );
        _tokenProvider = new CachingTokenProvider(new FakeTokenCredential());
        _fabricWorkspaceId = "33333333-3333-3333-3333-333333333333";

        var result = RunInspection(
            "scanner-progress.json",
            "{\"scannerapi\":[\"{context-fabricworkspace}\"]}",
            JsonNode.Parse("{\"workspaces\":[{\"id\":\"ws-1\"}]}")!);

        Assert.That(result.TestResults.Single().Pass, Is.True);
        Assert.That(result.Messages, Has.Some.Contains("Operator \"scannerapi\" - Starting workspace scan"));
        Assert.That(result.Messages, Has.Some.Contains("Polling scan 'scan-123': attempt 1/60"));
        Assert.That(result.Messages, Has.Some.Contains("Polling scan 'scan-123': attempt 5/60"));
        Assert.That(result.Messages, Has.Some.Contains("Polling scan 'scan-123': attempt 6/60, status 'Succeeded'"));
        Assert.That(result.Messages, Has.None.Contains("attempt 2/60"));
        Assert.That(result.Messages, Has.Some.Contains("Completed workspace scan 'scan-123'"));
    }

    private (List<TestResult> TestResults, List<string> Messages) RunInspection(string fileName, string logic, JsonNode expected)
    {
        var tempFilePath = CreateTempJsonFile(fileName);
        var fileSystem = new FabricLocalFileSystem(tempFilePath);
        var rules = new InspectionRules
        {
            Rules =
            [
                new Rule
                {
                    Id = "PROGRESS_TEST",
                    ItemType = "json",
                    Name = "Progress Test",
                    Description = "Progress test",
                    Disabled = false,
                    LogType = "warning",
                    Part = string.Empty,
                    Test = new Test
                    {
                        Logic = logic,
                        Data = new JsonObject(),
                        Expected = expected
                    }
                }
            ]
        };

        var inspector = new Inspector(rules, Registries, fileSystem);
        var messages = new List<string>();
        inspector.MessageIssued += (_, args) => messages.Add(args.Message);

        // Operators read their HttpClient/TokenProvider/FabricWorkspaceId/FabricItem
        // from InspectionContextHolder.Current. Build the ambient context from the
        // per-test fixture fields set by the calling test.
        var ambient = new InspectionContext
        {
            HttpClient = _httpClient ?? new HttpClient(),
            FabricWorkspaceId = _fabricWorkspaceId ?? string.Empty,
            FabricItem = _fabricItem,
            TokenProvider = _tokenProvider ?? new CachingTokenProvider(new FakeTokenCredential())
        };

        using var holderScope = InspectionContextHolder.PushScope(ambient);

        var results = inspector.Inspect();
        return (results, messages);
    }

    private string CreateTempJsonFile(string fileName)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");
        File.WriteAllText(tempFilePath, "{}", Encoding.UTF8);
        _tempPaths.Add(tempFilePath);
        return tempFilePath;
    }

    private static HttpClient CreateHttpClient(params HttpResponseMessage[] responses)
    {
        return new HttpClient(new QueueHttpMessageHandler(responses));
    }

    private static HttpClient CreateHttpClient(out QueueHttpMessageHandler handler, params HttpResponseMessage[] responses)
    {
        handler = new QueueHttpMessageHandler(responses);
        return new HttpClient(handler);
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<string> RequestedUris { get; } = [];

        public QueueHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri?.ToString() ?? string.Empty);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException($"No mock response configured for request '{request.RequestUri}'.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return Token;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Token);
        }
    }
}