using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests.Output
{
    [TestFixture]
    public class ResultOutputOrchestratorTests
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
        public void BuildWriters_ConsoleFormat_ReturnsConsoleWriter()
        {
            var args = CreateArgs("Console");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(writers, Has.Count.EqualTo(1));
            Assert.That(writers[0], Is.InstanceOf<ConsoleResultWriter>());
        }

        [Test]
        public void BuildWriters_JsonFormat_ReturnsJsonWriter()
        {
            var args = CreateArgs("JSON");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(writers, Has.Count.EqualTo(1));
            Assert.That(writers[0], Is.InstanceOf<JsonResultWriter>());
        }

        [Test]
        public void BuildWriters_HtmlFormat_ReturnsJsonPngAndHtmlWriters()
        {
            var args = CreateArgs("HTML");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            // HTML requires JSON (for data) and PNG (for wireframes), plus itself
            Assert.That(writers, Has.Count.EqualTo(3));
            Assert.That(writers[0], Is.InstanceOf<JsonResultWriter>());
            Assert.That(writers[1], Is.InstanceOf<PngResultWriter>());
            Assert.That(writers[2], Is.InstanceOf<HtmlResultWriter>());
        }

        [Test]
        public void BuildWriters_PngFormat_ReturnsPngWriter()
        {
            var args = CreateArgs("PNG");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(writers, Has.Count.EqualTo(1));
            Assert.That(writers[0], Is.InstanceOf<PngResultWriter>());
        }

        [Test]
        public void BuildWriters_ADOFormat_ReturnsOnlyConsoleWriter()
        {
            var args = CreateArgs("ADO");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            // ADO suppresses file-based writers; CONSOLEOutput is false since ADO is set
            Assert.That(writers, Has.Count.EqualTo(1));
            Assert.That(writers[0], Is.InstanceOf<ConsoleResultWriter>());
        }

        [Test]
        public void BuildWriters_GitHubFormat_ReturnsOnlyConsoleWriter()
        {
            var args = CreateArgs("GitHub");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(writers, Has.Count.EqualTo(1));
            Assert.That(writers[0], Is.InstanceOf<ConsoleResultWriter>());
        }

        [Test]
        public void BuildWriters_JsonAndConsole_ReturnsBothWriters()
        {
            var args = CreateArgs("JSON,Console");
            var orchestrator = CreateOrchestrator(args);
            var renderer = new StubPageRenderer();

            var writers = orchestrator.BuildWriters(renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(writers, Has.Count.EqualTo(2));
            Assert.That(writers[0], Is.InstanceOf<ConsoleResultWriter>());
            Assert.That(writers[1], Is.InstanceOf<JsonResultWriter>());
        }

        [Test]
        public async Task ExecuteAsync_ConsoleFormat_EmitsMessages()
        {
            var args = CreateArgs("Console");
            var messages = new List<(MessageTypeEnum Type, string Message)>();
            var itemMessages = new List<(string Path, MessageTypeEnum Type, string Message)>();
            var orchestrator = CreateOrchestrator(args,
                onMessage: (t, m) => messages.Add((t, m)),
                onItemMessage: (p, t, m) => itemMessages.Add((p, t, m)));

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "fail", Pass = false, LogType = MessageTypeEnum.Error, ItemPath = "item1" }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(itemMessages, Has.Count.EqualTo(1));
            Assert.That(itemMessages[0].Path, Is.EqualTo("item1"));
        }

        [Test]
        public async Task ExecuteAsync_JsonFormat_WritesJsonFile()
        {
            var args = CreateArgs("JSON");
            args.OutputPath = _tempDir;
            var orchestrator = CreateOrchestrator(args);

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            var jsonFiles = Directory.GetFiles(_tempDir, "*.json");
            Assert.That(jsonFiles, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_ErrorInWriter_EmitsErrorMessage()
        {
            // Use an empty output path that will cause JsonResultWriter to throw
            var args = CreateArgs("JSON");
            // Don't set OutputPath so OutputDirPath remains null

            var messages = new List<(MessageTypeEnum Type, string Message)>();
            var orchestrator = CreateOrchestrator(args,
                onMessage: (t, m) => messages.Add((t, m)));

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(messages.Any(m => m.Type == MessageTypeEnum.Error && m.Message.Contains("Could not output results")), Is.True);
        }

        [Test]
        public async Task ExecuteAsync_BuildTestedFilePath_WorkspaceAndItem()
        {
            var args = CreateArgs("JSON");
            args.OutputPath = _tempDir;
            args.FabricWorkspaceId = "ws-123";
            args.FabricItem = "myReport";

            var orchestrator = CreateOrchestrator(args);

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            // Verify JSON file contains the tested file path
            var jsonFile = Directory.GetFiles(_tempDir, "*.json").First();
            var content = File.ReadAllText(jsonFile);
            Assert.That(content, Does.Contain("Workspace: ws-123"));
            Assert.That(content, Does.Contain("Item: myReport"));
        }

        [Test]
        public async Task ExecuteAsync_BuildTestedFilePath_LocalItem()
        {
            var args = CreateArgs("JSON");
            args.OutputPath = _tempDir;
            args.FabricItem = @"C:\Reports\MyReport.pbir";

            var orchestrator = CreateOrchestrator(args);

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            var jsonFile = Directory.GetFiles(_tempDir, "*.json").First();
            var content = File.ReadAllText(jsonFile);
            Assert.That(content, Does.Contain(@"C:\\Reports\\MyReport.pbir"));
        }

        [Test]
        public async Task ExecuteAsync_CreatesOutputDirIfNotExists()
        {
            var outputDir = Path.Combine(_tempDir, "subdir");
            var args = CreateArgs("JSON");
            args.OutputPath = outputDir;

            var orchestrator = CreateOrchestrator(args);

            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true }
            };

            var renderer = new StubPageRenderer();
            await orchestrator.ExecuteAsync(results, renderer, Array.Empty<JsonLogicOperatorRegistry>());

            Assert.That(Directory.Exists(outputDir), Is.True);
        }

        private static Args CreateArgs(string formats)
        {
            return new Args { FormatsString = formats };
        }

        private static ResultOutputOrchestrator CreateOrchestrator(
            Args args,
            Action<MessageTypeEnum, string>? onMessage = null,
            Action<string, MessageTypeEnum, string>? onItemMessage = null)
        {
            return new ResultOutputOrchestrator(
                args,
                credential: null,
                onMessage: onMessage ?? ((_, _) => { }),
                onItemMessage: onItemMessage ?? ((_, _, _) => { }),
                onDialogMessage: (type, msg) => new MessageIssuedEventArgs(msg, type),
                createFileSystemAsync: () => Task.FromResult<IFabricFileSystem>(new StubFabricFileSystem()),
                deserialiseRules: _ => new InspectionRules { Rules = new List<Rule>() },
                runInspectionAsync: (_, _, _) => Task.FromResult<IEnumerable<TestResult>>(Array.Empty<TestResult>()),
                subscribeToProgress: _ => null,
                unsubscribeFromProgress: _ => { });
        }
    }
}
