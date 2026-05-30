using FabInspector.Core;

namespace FabInspector.ClientLibrary.Output
{
    internal class ConsoleResultWriter : IResultOutputWriter
    {
        public Task WriteAsync(OutputContext context)
        {
            foreach (var result in context.TestResults)
            {
                //TODO: use Test log type json property instead
                var msgType = result.Pass ? MessageTypeEnum.Information : result.LogType;
                var ruleSetName = string.IsNullOrWhiteSpace(result.RuleSetName) ? "Unknown" : result.RuleSetName;
                var message = string.Concat("[Ruleset: ", ruleSetName, "] ", result.Message);
                context.OnItemMessage(result.ItemPath ?? string.Empty, msgType, message);
            }

            if (context.TestResults.Any())
            {
                context.OnMessage(MessageTypeEnum.Information, string.Format("Test run summary: {0} errors, {1} warnings.",
                    context.TestResults.Count(_ => _.LogType == MessageTypeEnum.Error),
                    context.TestResults.Count(_ => _.LogType == MessageTypeEnum.Warning)));
            }
            else
            {
                context.OnMessage(MessageTypeEnum.Information, "Test run summary: No test results found.");
            }

            return Task.CompletedTask;
        }
    }
}
