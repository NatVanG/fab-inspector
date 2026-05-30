using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;
using System.Text.Json;

namespace FabInspector.ClientLibrary.Output
{
    internal class JsonResultWriter : IResultOutputWriter
    {
        public Task WriteAsync(OutputContext context)
        {
            var jsonFileName = string.Concat("TestRun_", context.TestRunId.ToString("N"), "_", context.Timestamp, ".json");

            if (string.IsNullOrEmpty(context.LocalOutputDirPath))
            {
                throw new ArgumentException("Directory with path \"{0}\" does not exist", context.LocalOutputDirPath);
            }

            var outputFilePath = Path.Combine(context.LocalOutputDirPath, jsonFileName);

            var testRun = new TestRun()
            {
                Id = context.TestRunId,
                CompletionTime = DateTime.Now,
                TestedFilePath = context.TestedFilePath,
                RulesFilePath = context.RulesFilePath,
                RulesCatalogPath = context.RulesCatalogPath,
                Verbose = context.Verbose,
                Results = context.TestResults
            };

            context.JsonTestRun = JsonSerializer.Serialize(testRun);

            context.OnMessage(MessageTypeEnum.Information, string.Format("Writing JSON output to file at \"{0}\".", outputFilePath));
            File.WriteAllText(outputFilePath, context.JsonTestRun, System.Text.Encoding.UTF8);

            if (context.IsOneLakeOutput)
            {
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var relativeJsonPath = Path.Combine(dateFolder, jsonFileName);
                context.OutputArtifacts.Add((outputFilePath, relativeJsonPath));
            }

            return Task.CompletedTask;
        }
    }
}
