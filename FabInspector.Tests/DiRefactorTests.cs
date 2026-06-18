using System.Net;
using System.Net.Http;
using System.Text;
using Azure.Core;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Hosting;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Inspection;
using FabInspector.Core.Output;
using FabInspector.Operators;
using Microsoft.Extensions.DependencyInjection;

namespace FabInspector.Tests;

[TestFixture]
[NonParallelizable]
public class DiRefactorTests
{
    [Test]
    public void AddFabInspectorCore_RegistersSingletonHttpClient()
    {
        var services = new ServiceCollection();

        var returned = services.AddFabInspectorCore();
        using var provider = services.BuildServiceProvider();

        var fromRoot1 = provider.GetRequiredService<HttpClient>();
        var fromRoot2 = provider.GetRequiredService<HttpClient>();
        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<HttpClient>();

        Assert.That(returned, Is.SameAs(services));
        Assert.That(fromRoot2, Is.SameAs(fromRoot1));
        Assert.That(fromScope, Is.SameAs(fromRoot1));
    }

    [Test]
    public void InspectionContextHolder_PushScope_RestoresPriorContext()
    {
        var ctx1 = CreateAmbientContext(workspaceId: "ws-1");
        var ctx2 = CreateAmbientContext(workspaceId: "ws-2");

        Assert.That(InspectionContextHolder.Current, Is.Null);

        using (InspectionContextHolder.PushScope(ctx1))
        {
            Assert.That(InspectionContextHolder.Current, Is.SameAs(ctx1));

            using (InspectionContextHolder.PushScope(ctx2))
            {
                Assert.That(InspectionContextHolder.Current, Is.SameAs(ctx2));
            }

            Assert.That(InspectionContextHolder.Current, Is.SameAs(ctx1));
        }

        Assert.That(InspectionContextHolder.Current, Is.Null);
    }

    [Test]
    public void InspectionContextHolder_Require_ThrowsWhenNoScope()
    {
        Assert.That(InspectionContextHolder.Current, Is.Null);

        var ex = Assert.Throws<InvalidOperationException>(() => InspectionContextHolder.Require("apiget"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("InspectionContextHolder.Current is not configured"));
        Assert.That(ex.Message, Does.Contain("Operator 'apiget'"));
    }

    [Test]
    public void InspectionContextHolder_ReportOperatorProgress_UsesAmbientReporterAndItemPath()
    {
        var captured = new List<MessageIssuedEventArgs>();
        var context = CreateAmbientContext(workspaceId: "ws");
        context.RuleName = "Rule A";
        context.ItemPath = "fake/path/item.json";
        context.MessageReporter = new CaptureReporter(captured);

        using (InspectionContextHolder.PushScope(context))
        {
            InspectionContextHolder.ReportOperatorProgress("apiget", "Starting call");
        }

        Assert.That(captured, Has.Count.EqualTo(1));
        Assert.That(captured[0].ItemPath, Is.EqualTo("fake/path/item.json"));
        Assert.That(captured[0].MessageType, Is.EqualTo(MessageTypeEnum.Information));
        Assert.That(captured[0].Message, Does.Contain("Rule \"Rule A\""));
        Assert.That(captured[0].Message, Does.Contain("Operator \"apiget\""));
        Assert.That(captured[0].Message, Does.Contain("Starting call"));
    }

    [Test]
    public void Inspector_DoesNotOverwritePreconfiguredScopedItemTypes()
    {
        var fileSystem = new FabricLocalFileSystem();
        fileSystem.ScopedItemTypes = ["json", "report"];

        var rules = new InspectionRules
        {
            Rules =
            [
                new Rule
                {
                    Name = "Rule A",
                    ItemType = "scannerapi",
                    Test = new Test
                    {
                        Logic = "true",
                        Expected = true
                    }
                }
            ]
        };

        _ = new Inspector(rules, Array.Empty<JsonLogicOperatorRegistry>(), fileSystem);

        Assert.That(fileSystem.ScopedItemTypes, Is.EquivalentTo(["json", "report"]));
    }

    [Test]
    public void InspectionEngine_GetScopedItemTypes_UnionsRuleTypesAcrossResolvedRuleSets()
    {
        var resolvedRuleSets = new[]
        {
            new ResolvedRuleSet
            {
                Name = "A",
                SourcePath = "a.json",
                Rules = new InspectionRules
                {
                    Rules =
                    [
                        new Rule
                        {
                            Name = "Rule 1",
                            ItemType = "json|report",
                            Test = new Test { Logic = "true", Expected = true }
                        }
                    ]
                }
            },
            new ResolvedRuleSet
            {
                Name = "B",
                SourcePath = "b.json",
                Rules = new InspectionRules
                {
                    Rules =
                    [
                        new Rule
                        {
                            Name = "Rule 2",
                            ItemType = "REPORT|scannerapi|json",
                            Test = new Test { Logic = "true", Expected = true }
                        }
                    ]
                }
            }
        };

        var scopedItemTypes = InspectionEngine.GetScopedItemTypes(resolvedRuleSets);

        Assert.That(scopedItemTypes, Is.EquivalentTo(["json", "report", "scannerapi"]));
    }

    [Test]
    public async Task InspectionEngine_RunAndReturnResultsAsync_IsolatesPerRunStateAcrossConcurrentEngines()
    {
        var registries = new[]
        {
            new JsonLogicOperatorRegistry(
                new FabInspectorSerializerContext(),
                [new ApiGetOperator()])
        };

        var itemPath = CreateTempJsonFile("{}", ".json");
        var rulesPath1 = CreateRulesFile("one", "{\"value\":[1]}");
        var rulesPath2 = CreateRulesFile("two", "{\"value\":[2]}");

        try
        {
            var args1 = CreateLocalArgs(itemPath, rulesPath1);
            var args2 = CreateLocalArgs(itemPath, rulesPath2);

            var engine1 = new InspectionEngine(CreateHttpClient(CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[1]}")));
            var engine2 = new InspectionEngine(CreateHttpClient(CreateJsonResponse(HttpStatusCode.OK, "{\"value\":[2]}")));

            var tokenProvider = new CachingTokenProvider(new FakeTokenCredential());
            var renderer = new NoOpReportPageWireframeRenderer();

            var runTask1 = engine1.RunAndReturnResultsAsync(args1, tokenProvider, renderer, registries);
            var runTask2 = engine2.RunAndReturnResultsAsync(args2, tokenProvider, renderer, registries);

            var runs = await Task.WhenAll(runTask1, runTask2);

            var results1 = runs[0].Results.ToList();
            var results2 = runs[1].Results.ToList();

            Assert.That(results1, Has.Count.EqualTo(1));
            Assert.That(results2, Has.Count.EqualTo(1));
            Assert.That(results1[0].Pass, Is.True);
            Assert.That(results2[0].Pass, Is.True);
            Assert.That(engine1.ErrorCount, Is.EqualTo(0));
            Assert.That(engine2.ErrorCount, Is.EqualTo(0));
            Assert.That(engine1.WarningCount, Is.EqualTo(0));
            Assert.That(engine2.WarningCount, Is.EqualTo(0));
        }
        finally
        {
            DeleteIfExists(itemPath);
            DeleteIfExists(rulesPath1);
            DeleteIfExists(rulesPath2);
        }
    }

    private static Args CreateLocalArgs(string itemPath, string rulesPath)
    {
        var args = new Args
        {
            FabricItem = itemPath,
            RulesFilePath = rulesPath,
            AuthMethod = "local"
        };
        args.VerboseString = "true";
        return args;
    }

    private static InspectionContext CreateAmbientContext(string workspaceId)
    {
        return new InspectionContext
        {
            HttpClient = new HttpClient(),
            FabricWorkspaceId = workspaceId,
            FabricItem = "item-id",
            TokenProvider = new CachingTokenProvider(new FakeTokenCredential())
        };
    }

    private static string CreateTempJsonFile(string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string CreateRulesFile(string suffix, string expectedJson)
    {
        var rules = """
{
  "rules": [
    {
      "id": "DI_TEST",
      "itemType": "json",
      "name": "DI Test",
      "description": "DI test",
      "disabled": false,
      "logType": "warning",
      "part": "",
      "test": [
        { "apiget": ["https://api.fabric.microsoft.com/v1/workspaces"] },
        {},
        __EXPECTED__
      ]
    }
  ]
}
""";

        var content = rules.Replace("__EXPECTED__", expectedJson);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{suffix}_rules.json");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static HttpClient CreateHttpClient(params HttpResponseMessage[] responses)
    {
        return new HttpClient(new QueueHttpMessageHandler(responses));
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueueHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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

    private sealed class CaptureReporter : IInspectionMessageReporter
    {
        private readonly List<MessageIssuedEventArgs> _captured;

        public CaptureReporter(List<MessageIssuedEventArgs> captured)
        {
            _captured = captured;
        }

        public void Report(MessageIssuedEventArgs args)
        {
            _captured.Add(args);
        }
    }

    private sealed class NoOpReportPageWireframeRenderer : IReportPageWireframeRenderer
    {
        public void DrawReportPages(IEnumerable<TestResult> fieldMapResults, IEnumerable<TestResult> testResults, string outputDir, string testedFilePath)
        {
        }

        public string ConvertBitmapToBase64(string bitmapPath)
        {
            return string.Empty;
        }
    }
}
