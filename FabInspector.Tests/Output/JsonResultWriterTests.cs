using FabInspector.ClientLibrary.Output;
using FabInspector.Core;
using FabInspector.Core.Output;
using System.Text.Json;

namespace FabInspector.Tests.Output
{
    [TestFixture]
    public class JsonResultWriterTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FabInspectorTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public async Task WriteAsync_WritesJsonFileToOutputDir()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results);
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            var jsonFiles = Directory.GetFiles(_tempDir, "*.json");
            Assert.That(jsonFiles, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task WriteAsync_SetsJsonTestRunOnContext()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results);
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            Assert.That(context.JsonTestRun, Is.Not.Empty);
            var testRun = JsonSerializer.Deserialize<TestRun>(context.JsonTestRun);
            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun!.Results.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task WriteAsync_JsonContainsTestedFilePath()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, testedFilePath: "Workspace: ws123 | Item: myReport");
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            Assert.That(context.JsonTestRun, Does.Contain("Workspace: ws123"));
        }

        [Test]
        public async Task WriteAsync_FileNameIncludesFabricItem()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, fabricItem: "MyReport.pbir");
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            var jsonFiles = Directory.GetFiles(_tempDir, "*.json");
            Assert.That(Path.GetFileName(jsonFiles[0]), Does.Contain("MyReport"));
        }

        [Test]
        public async Task WriteAsync_OneLakeOutput_AddsToOutputArtifacts()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, isOneLakeOutput: true);
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            Assert.That(context.OutputArtifacts, Has.Count.EqualTo(1));
            Assert.That(context.OutputArtifacts[0].RelativePath, Does.EndWith(".json"));
            Assert.That(context.OutputArtifacts[0].RelativePath, Does.Match(@"\d{4}-\d{2}-\d{2}[/\\]TestRun_[a-f0-9]{32}_\d{6}\.json"));
        }

        [Test]
        public async Task WriteAsync_LocalOutput_DoesNotAddToOutputArtifacts()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, isOneLakeOutput: false);
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            Assert.That(context.OutputArtifacts, Is.Empty);
        }

        [Test]
        public void WriteAsync_EmptyOutputDir_ThrowsArgumentException()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = new OutputContext
            {
                TestResults = results,
                LocalOutputDirPath = string.Empty,
                IsOneLakeOutput = false,
                TestedFilePath = "test",
            };

            var writer = new JsonResultWriter();
            Assert.ThrowsAsync<ArgumentException>(async () => await writer.WriteAsync(context));
        }

        [Test]
        public async Task WriteAsync_EmitsInformationMessage()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var messages = new List<string>();
            var context = CreateContext(results, onMessage: (_, msg) => messages.Add(msg));
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            Assert.That(messages.Any(m => m.Contains("Writing JSON output")), Is.True);
        }

        [Test]
        public async Task WriteAsync_OneLakeOutput_FileNameIncludesTestRunIdAndTimestamp()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, isOneLakeOutput: true, fabricItem: "Report.pbir");
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            var jsonFiles = Directory.GetFiles(_tempDir, "*.json");
            // OneLake files use TestRunId and timestamp: TestRun_{guid}_{HHmmss}.json
            Assert.That(Path.GetFileName(jsonFiles[0]), Does.Match(@"TestRun_[a-f0-9]{32}_\d{6}\.json"));
        }

        [Test]
        public async Task WriteAsync_OneLakeOutput_SetsTestRunIdOnSerializedTestRun()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var context = CreateContext(results, isOneLakeOutput: true);
            var writer = new JsonResultWriter();
            await writer.WriteAsync(context);

            var testRun = JsonSerializer.Deserialize<TestRun>(context.JsonTestRun);
            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun!.Id, Is.EqualTo(context.TestRunId));
        }

        private OutputContext CreateContext(
            IEnumerable<TestResult> results,
            bool isOneLakeOutput = false,
            string? fabricItem = null,
            string testedFilePath = "test",
            Action<MessageTypeEnum, string>? onMessage = null)
        {
            return new OutputContext
            {
                TestResults = results,
                LocalOutputDirPath = _tempDir,
                IsOneLakeOutput = isOneLakeOutput,
                TestedFilePath = testedFilePath,
                FabricItem = fabricItem,
                FabricWorkspaceId = "ws-test",
                RulesFilePath = "rules.json",
                Verbose = false,
                OnMessage = onMessage ?? ((_, _) => { }),
            };
        }
    }
}
