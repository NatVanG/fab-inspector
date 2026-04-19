using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Output;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests.Output
{
    [TestFixture]
    public class HtmlResultWriterTests
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
        public async Task WriteAsync_WritesHtmlFileToOutputDir()
        {
            var renderer = new StubPageRenderer();
            var context = CreateContext(jsonTestRun: "{\"results\":[]}");

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            var htmlFile = Path.Combine(_tempDir, Constants.TestRunHTMLFileName);
            Assert.That(File.Exists(htmlFile), Is.True);
        }

        [Test]
        public async Task WriteAsync_HtmlContainsJsonTestRun()
        {
            var renderer = new StubPageRenderer();
            var jsonPayload = "{\"test\":\"data\"}";
            var context = CreateContext(jsonTestRun: jsonPayload);

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            var htmlFile = Path.Combine(_tempDir, Constants.TestRunHTMLFileName);
            var html = File.ReadAllText(htmlFile);
            Assert.That(html, Does.Contain(jsonPayload));
        }

        [Test]
        public async Task WriteAsync_HtmlContainsVersionString()
        {
            var renderer = new StubPageRenderer();
            var context = CreateContext(jsonTestRun: "{}");

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            var htmlFile = Path.Combine(_tempDir, Constants.TestRunHTMLFileName);
            var html = File.ReadAllText(htmlFile);
            Assert.That(html, Does.Contain("Fab Inspector v"));
        }

        [Test]
        public async Task WriteAsync_HtmlContainsLogoBase64()
        {
            var renderer = new StubPageRenderer { Base64Result = "TEST_BASE64" };
            var context = CreateContext(jsonTestRun: "{}");

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            var htmlFile = Path.Combine(_tempDir, Constants.TestRunHTMLFileName);
            var html = File.ReadAllText(htmlFile);
            Assert.That(html, Does.Contain("TEST_BASE64"));
        }

        [Test]
        public async Task WriteAsync_OneLakeOutput_AddsToOutputArtifacts()
        {
            var renderer = new StubPageRenderer();
            var context = CreateContext(jsonTestRun: "{}", isOneLakeOutput: true);

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(context.OutputArtifacts, Has.Count.EqualTo(1));
            Assert.That(context.OutputArtifacts[0].RelativePath, Is.EqualTo(Constants.TestRunHTMLFileName));
        }

        [Test]
        public async Task WriteAsync_LocalOutput_DoesNotAddToOutputArtifacts()
        {
            var renderer = new StubPageRenderer();
            var context = CreateContext(jsonTestRun: "{}", isOneLakeOutput: false);

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(context.OutputArtifacts, Is.Empty);
        }

        [Test]
        public async Task WriteAsync_EmitsInformationMessage()
        {
            var renderer = new StubPageRenderer();
            var messages = new List<string>();
            var context = CreateContext(jsonTestRun: "{}", onMessage: (_, msg) => messages.Add(msg));

            var writer = new HtmlResultWriter(renderer);
            await writer.WriteAsync(context);

            Assert.That(messages.Any(m => m.Contains("Writing HTML output")), Is.True);
        }

        private OutputContext CreateContext(
            string jsonTestRun,
            bool isOneLakeOutput = false,
            Action<MessageTypeEnum, string>? onMessage = null)
        {
            return new OutputContext
            {
                TestResults = Array.Empty<TestResult>(),
                LocalOutputDirPath = _tempDir,
                IsOneLakeOutput = isOneLakeOutput,
                TestedFilePath = "test",
                CONSOLEOutput = true, // Prevent OpenUrl call
                DeleteOutputDirOnExit = false,
                JsonTestRun = jsonTestRun,
                OnMessage = onMessage ?? ((_, _) => { }),
            };
        }
    }
}
