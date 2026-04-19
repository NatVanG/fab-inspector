using FabInspector.Core;
using FabInspector.Core.Exceptions;
using FabInspector.Core.Output;

namespace FabInspector.ClientLibrary.Output
{
    internal class PngResultWriter : IResultOutputWriter
    {
        private readonly IReportPageWireframeRenderer _pageRenderer;
        private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
        private readonly Func<Task<IFabricFileSystem>> _createFileSystemAsync;
        private readonly Func<string, InspectionRules> _deserialiseRules;
        private readonly Func<InspectionRules, IEnumerable<JsonLogicOperatorRegistry>, IFabricFileSystem, Task<IEnumerable<TestResult>>> _runInspectionAsync;
        private readonly Func<IFabricFileSystem, FabricRemoteFileSystem?> _subscribeToProgress;
        private readonly Action<FabricRemoteFileSystem?> _unsubscribeFromProgress;

        public PngResultWriter(
            IReportPageWireframeRenderer pageRenderer,
            IEnumerable<JsonLogicOperatorRegistry> registries,
            Func<Task<IFabricFileSystem>> createFileSystemAsync,
            Func<string, InspectionRules> deserialiseRules,
            Func<InspectionRules, IEnumerable<JsonLogicOperatorRegistry>, IFabricFileSystem, Task<IEnumerable<TestResult>>> runInspectionAsync,
            Func<IFabricFileSystem, FabricRemoteFileSystem?> subscribeToProgress,
            Action<FabricRemoteFileSystem?> unsubscribeFromProgress)
        {
            _pageRenderer = pageRenderer;
            _registries = registries;
            _createFileSystemAsync = createFileSystemAsync;
            _deserialiseRules = deserialiseRules;
            _runInspectionAsync = runInspectionAsync;
            _subscribeToProgress = subscribeToProgress;
            _unsubscribeFromProgress = unsubscribeFromProgress;
        }

        public async Task WriteAsync(OutputContext context)
        {
            // Only run for report-related rules
            if (!context.TestResults.Any(_ =>
                (_.RuleItemType?.Contains("Report", StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (_.RuleItemType?.Contains("report_deprecated", StringComparison.InvariantCultureIgnoreCase) ?? false)))
            {
                return;
            }

            var fieldMapFileSystem = await _createFileSystemAsync().ConfigureAwait(false);
            var fieldMapRemoteFs = _subscribeToProgress(fieldMapFileSystem);
            IEnumerable<TestResult> fieldMapResults;

            try
            {
                var fieldMapPathRules = _deserialiseRules(Constants.ReportPageFieldMapFilePath);
                fieldMapResults = await _runInspectionAsync(fieldMapPathRules, _registries, fieldMapFileSystem).ConfigureAwait(false);
            }
            finally
            {
                _unsubscribeFromProgress(fieldMapRemoteFs);
            }

            var outputPNGDirPath = Path.Combine(context.LocalOutputDirPath, Constants.PNGOutputDir);

            if (Directory.Exists(outputPNGDirPath))
            {
                if (context.OverwriteOutput)
                {
                    Directory.Delete(outputPNGDirPath, true);
                }
                else
                {
                    if (context.IsOneLakeOutput)
                    {
                        throw new PBIRInspectorException(string.Format("Output directory already exists at \"{0}\" and overwriteoutput is false.", outputPNGDirPath));
                    }

                    var eventArgs = context.OnDialogMessage(MessageTypeEnum.Dialog, string.Format("Directory already exists at \"{0}\". Do you want to overwrite existing content?", outputPNGDirPath));
                    if (eventArgs.DialogOKResponse)
                    {
                        Directory.Delete(outputPNGDirPath, true);
                    }
                    else
                    {
                        context.OnMessage(MessageTypeEnum.Information, "Skipping PNG output as directory already exists and overwrite not set.");
                        return;
                    }
                }
            }

            Directory.CreateDirectory(outputPNGDirPath);
            context.OnMessage(MessageTypeEnum.Information, string.Format("Writing report page wireframe images to files at \"{0}\".", outputPNGDirPath));
            _pageRenderer.DrawReportPages(fieldMapResults, context.TestResults, outputPNGDirPath);

            if (context.IsOneLakeOutput)
            {
                foreach (var pngPath in Directory.GetFiles(outputPNGDirPath, "*.png", SearchOption.TopDirectoryOnly))
                {
                    var relativePngPath = Path.Combine(Constants.PNGOutputDir, Path.GetFileName(pngPath));
                    context.OutputArtifacts.Add((pngPath, relativePngPath));
                }
            }
        }
    }
}
