using FabInspector.Core;

namespace FabInspector.ClientLibrary.Output
{
    internal class ConsoleResultWriter : IResultOutputWriter
    {
        public Task WriteAsync(OutputContext context)
        {
            foreach (var result in context.TestResults)
            {
                var msgType = GetOutputMessageType(result);
                context.OnItemMessage(result.ItemPath ?? string.Empty, msgType, result.Message);
            }

            if (context.TestResults.Any())
            {
                context.OnMessage(MessageTypeEnum.Information, string.Format("Test run summary: {0} errors, {1} warnings, {2} info.",
                    context.TestResults.Count(_ => GetOutputMessageType(_) == MessageTypeEnum.Error),
                    context.TestResults.Count(_ => GetOutputMessageType(_) == MessageTypeEnum.Warning),
                    context.TestResults.Count(_ => GetOutputMessageType(_) == MessageTypeEnum.Information)));
            }
            else
            {
                context.OnMessage(MessageTypeEnum.Information, "Test run summary: No test results found.");
            }

            return Task.CompletedTask;
        }

        private static MessageTypeEnum GetOutputMessageType(TestResult result)
        {
            return result.Pass ? MessageTypeEnum.Information : result.LogType;
        }
    }
}
