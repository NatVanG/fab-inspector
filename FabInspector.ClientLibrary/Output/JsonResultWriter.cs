using FabInspector.Core;
using FabInspector.Core.Output;
using System.Text.Json;

namespace FabInspector.ClientLibrary.Output
{
    internal class JsonResultWriter : IResultOutputWriter
    {
        public Task WriteAsync(OutputContext context)
        {
            var outputFileIdentifier = !string.IsNullOrWhiteSpace(context.FabricItem)
                ? Path.GetFileNameWithoutExtension(context.FabricItem)
                : context.FabricWorkspaceId;
            var timestampSuffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var jsonFileName = context.IsOneLakeOutput
                ? string.Concat("TestRun_", outputFileIdentifier, "_", timestampSuffix, ".json")
                : string.Concat("TestRun_", outputFileIdentifier, ".json");

            if (string.IsNullOrEmpty(context.LocalOutputDirPath))
            {
                throw new ArgumentException("Directory with path \"{0}\" does not exist", context.LocalOutputDirPath);
            }

            var outputFilePath = Path.Combine(context.LocalOutputDirPath, jsonFileName);

            var testRun = new TestRun()
            {
                CompletionTime = DateTime.Now,
                TestedFilePath = context.TestedFilePath,
                RulesFilePath = context.RulesFilePath,
                Verbose = context.Verbose,
                Results = context.TestResults
            };

            context.JsonTestRun = JsonSerializer.Serialize(testRun);

            context.OnMessage(MessageTypeEnum.Information, string.Format("Writing JSON output to file at \"{0}\".", outputFilePath));
            File.WriteAllText(outputFilePath, context.JsonTestRun, System.Text.Encoding.UTF8);

            if (context.IsOneLakeOutput)
            {
                context.OutputArtifacts.Add((outputFilePath, jsonFileName));
            }

            return Task.CompletedTask;
        }
    }
}
