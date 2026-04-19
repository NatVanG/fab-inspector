using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.ClientLibrary.Output
{
    internal class OutputContext
    {
        public required IEnumerable<TestResult> TestResults { get; init; }
        public required string LocalOutputDirPath { get; init; }
        public required bool IsOneLakeOutput { get; init; }
        public required string TestedFilePath { get; init; }
        public string? RulesFilePath { get; init; }
        public bool Verbose { get; init; }
        public bool OverwriteOutput { get; init; }
        public string? FabricItem { get; init; }
        public string? FabricWorkspaceId { get; init; }
        public bool DeleteOutputDirOnExit { get; init; }
        public bool CONSOLEOutput { get; init; }

        public Action<MessageTypeEnum, string> OnMessage { get; init; } = (_, _) => { };
        public Action<string, MessageTypeEnum, string> OnItemMessage { get; init; } = (_, _, _) => { };
        public Func<MessageTypeEnum, string, MessageIssuedEventArgs> OnDialogMessage { get; init; } = (type, msg) => new MessageIssuedEventArgs(msg, type);

        public List<(string LocalPath, string RelativePath)> OutputArtifacts { get; } = new();

        /// <summary>
        /// Set by JsonResultWriter, consumed by HtmlResultWriter.
        /// </summary>
        public string JsonTestRun { get; set; } = string.Empty;
    }
}
