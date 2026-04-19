using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Output;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests.Output
{
    [TestFixture]
    public class PngResultWriterTests
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
        public async Task WriteAsync_NoReportResults_SkipsProcessing()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Lakehouse" }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(renderer.DrawCalls, Is.Empty);
        }

        [Test]
        public async Task WriteAsync_ReportResults_CallsDrawReportPages()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(renderer.DrawCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task WriteAsync_ReportDeprecatedResults_CallsDrawReportPages()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "report_deprecated" }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(renderer.DrawCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task WriteAsync_CreatesPngOutputDirectory()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            var pngDir = Path.Combine(_tempDir, Constants.PNGOutputDir);
            Assert.That(Directory.Exists(pngDir), Is.True);
        }

        [Test]
        public async Task WriteAsync_PassesCorrectOutputDirToRenderer()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            var expectedDir = Path.Combine(_tempDir, Constants.PNGOutputDir);
            Assert.That(renderer.DrawCalls[0].OutputDir, Is.EqualTo(expectedDir));
        }

        [Test]
        public async Task WriteAsync_OverwriteTrue_DeletesExistingPngDir()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };

            // Pre-create the PNG output directory with a marker file
            var pngDir = Path.Combine(_tempDir, Constants.PNGOutputDir);
            Directory.CreateDirectory(pngDir);
            File.WriteAllText(Path.Combine(pngDir, "old.txt"), "old");

            var context = CreateContext(results, overwriteOutput: true);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            // Old file should be gone, directory recreated
            Assert.That(File.Exists(Path.Combine(pngDir, "old.txt")), Is.False);
            Assert.That(Directory.Exists(pngDir), Is.True);
        }

        [Test]
        public async Task WriteAsync_EmitsInformationMessage()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };

            var messages = new List<string>();
            var context = CreateContext(results, onMessage: (_, msg) => messages.Add(msg));
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(messages.Any(m => m.Contains("Writing report page wireframe images")), Is.True);
        }

        [Test]
        public async Task WriteAsync_PassesFieldMapResultsToRenderer()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = "Report" }
            };
            var fieldMapResults = new List<TestResult>
            {
                new() { RuleName = "FM1", Message = "field", Pass = true }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer, fieldMapResults: fieldMapResults);
            await writer.WriteAsync(context);

            Assert.That(renderer.DrawCalls[0].FieldMapResults, Is.SameAs(fieldMapResults));
        }

        [Test]
        public async Task WriteAsync_NullRuleItemType_DoesNotThrow()
        {
            var renderer = new StubPageRenderer();
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, RuleItemType = null }
            };

            var context = CreateContext(results);
            var writer = CreateWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(renderer.DrawCalls, Is.Empty);
        }

        private OutputContext CreateContext(
            IEnumerable<TestResult> results,
            bool isOneLakeOutput = false,
            bool overwriteOutput = false,
            Action<MessageTypeEnum, string>? onMessage = null)
        {
            return new OutputContext
            {
                TestResults = results,
                LocalOutputDirPath = _tempDir,
                IsOneLakeOutput = isOneLakeOutput,
                TestedFilePath = "test",
                OverwriteOutput = overwriteOutput,
                OnMessage = onMessage ?? ((_, _) => { }),
                OnDialogMessage = (type, msg) => new MessageIssuedEventArgs(msg, type),
            };
        }

        private static PngResultWriter CreateWriter(
            StubPageRenderer renderer,
            IEnumerable<TestResult>? fieldMapResults = null)
        {
            var stubFieldMapResults = fieldMapResults ?? new List<TestResult>();
            var stubRules = new InspectionRules { Rules = new List<Rule>() };
            var stubFileSystem = new StubFabricFileSystem();

            return new PngResultWriter(
                renderer,
                registries: Array.Empty<JsonLogicOperatorRegistry>(),
                createFileSystemAsync: () => Task.FromResult<IFabricFileSystem>(stubFileSystem),
                deserialiseRules: _ => stubRules,
                runInspectionAsync: (_, _, _) => Task.FromResult<IEnumerable<TestResult>>(stubFieldMapResults),
                subscribeToProgress: _ => null,
                unsubscribeFromProgress: _ => { });
        }
    }
}
