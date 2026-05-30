using FabInspector.ClientLibrary.Output;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests.Output
{
    [TestFixture]
    public class ConsoleResultWriterTests
    {
        [Test]
        public async Task WriteAsync_EmitsItemMessageForEachResult()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "msg1", Pass = true, LogType = MessageTypeEnum.Warning, ItemPath = "path1" },
                new() { RuleName = "R2", Message = "msg2", Pass = false, LogType = MessageTypeEnum.Error, ItemPath = "path2" }
            };

            var itemMessages = new List<(string ItemPath, MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onItemMessage: (path, type, msg) => itemMessages.Add((path, type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(itemMessages, Has.Count.EqualTo(2));
            Assert.That(itemMessages[0].ItemPath, Is.EqualTo("path1"));
            Assert.That(itemMessages[1].ItemPath, Is.EqualTo("path2"));
        }

        [Test]
        public async Task WriteAsync_PassingResult_UsesInformationType()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "ok", Pass = true, LogType = MessageTypeEnum.Error }
            };

            var itemMessages = new List<(string ItemPath, MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onItemMessage: (path, type, msg) => itemMessages.Add((path, type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(itemMessages[0].Type, Is.EqualTo(MessageTypeEnum.Information));
        }

        [Test]
        public async Task WriteAsync_FailingResult_UsesResultLogType()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "fail", Pass = false, LogType = MessageTypeEnum.Warning }
            };

            var itemMessages = new List<(string ItemPath, MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onItemMessage: (path, type, msg) => itemMessages.Add((path, type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(itemMessages[0].Type, Is.EqualTo(MessageTypeEnum.Warning));
        }

        [Test]
        public async Task WriteAsync_WithResults_EmitsSummaryWithCounts()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "e1", Pass = false, LogType = MessageTypeEnum.Error },
                new() { RuleName = "R2", Message = "w1", Pass = false, LogType = MessageTypeEnum.Warning },
                new() { RuleName = "R3", Message = "w2", Pass = false, LogType = MessageTypeEnum.Warning },
            };

            var messages = new List<(MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onMessage: (type, msg) => messages.Add((type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            var summary = messages.FirstOrDefault(m => m.Message.Contains("Test run summary"));
            Assert.That(summary.Message, Does.Contain("1 errors"));
            Assert.That(summary.Message, Does.Contain("2 warnings"));
        }

        [Test]
        public async Task WriteAsync_EmptyResults_EmitsNoResultsFoundSummary()
        {
            var results = new List<TestResult>();
            var messages = new List<(MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onMessage: (type, msg) => messages.Add((type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(messages.Any(m => m.Message.Contains("No test results found")), Is.True);
        }

        [Test]
        public async Task WriteAsync_NullItemPath_UsesEmptyString()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "msg", Pass = true, LogType = MessageTypeEnum.Information, ItemPath = null }
            };

            var itemMessages = new List<(string ItemPath, MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onItemMessage: (path, type, msg) => itemMessages.Add((path, type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(itemMessages[0].ItemPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task WriteAsync_AlwaysPrefixesMessageWithRulesetName()
        {
            var results = new List<TestResult>
            {
                new() { RuleName = "R1", Message = "first", Pass = false, LogType = MessageTypeEnum.Error, RuleSetName = "Org baseline" },
                new() { RuleName = "R2", Message = "second", Pass = false, LogType = MessageTypeEnum.Warning, RuleSetName = null }
            };

            var itemMessages = new List<(string ItemPath, MessageTypeEnum Type, string Message)>();
            var context = CreateContext(results, onItemMessage: (path, type, msg) => itemMessages.Add((path, type, msg)));

            var writer = new ConsoleResultWriter();
            await writer.WriteAsync(context);

            Assert.That(itemMessages[0].Message, Is.EqualTo("[Ruleset: Org baseline] first"));
            Assert.That(itemMessages[1].Message, Is.EqualTo("[Ruleset: Unknown] second"));
        }

        private static OutputContext CreateContext(
            IEnumerable<TestResult> results,
            Action<MessageTypeEnum, string>? onMessage = null,
            Action<string, MessageTypeEnum, string>? onItemMessage = null)
        {
            return new OutputContext
            {
                TestResults = results,
                LocalOutputDirPath = string.Empty,
                IsOneLakeOutput = false,
                TestedFilePath = "test",
                OnMessage = onMessage ?? ((_, _) => { }),
                OnItemMessage = onItemMessage ?? ((_, _, _) => { }),
            };
        }
    }
}
