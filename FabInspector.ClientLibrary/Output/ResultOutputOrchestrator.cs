using Azure.Core;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.ClientLibrary.Output
{
    internal class ResultOutputOrchestrator
    {
        private readonly Args _args;
        private readonly TokenCredential? _credential;
        private readonly Action<MessageTypeEnum, string> _onMessage;
        private readonly Action<string, MessageTypeEnum, string> _onItemMessage;
        private readonly Func<MessageTypeEnum, string, MessageIssuedEventArgs> _onDialogMessage;
        private readonly Func<Task<IFabricFileSystem>> _createFileSystemAsync;
        private readonly Func<string, InspectionRules> _deserialiseRules;
        private readonly Func<InspectionRules, IEnumerable<JsonLogicOperatorRegistry>, IFabricFileSystem, Task<IEnumerable<TestResult>>> _runInspectionAsync;
        private readonly Func<IFabricFileSystem, FabricRemoteFileSystem?> _subscribeToProgress;
        private readonly Action<FabricRemoteFileSystem?> _unsubscribeFromProgress;

        public ResultOutputOrchestrator(
            Args args,
            TokenCredential? credential,
            Action<MessageTypeEnum, string> onMessage,
            Action<string, MessageTypeEnum, string> onItemMessage,
            Func<MessageTypeEnum, string, MessageIssuedEventArgs> onDialogMessage,
            Func<Task<IFabricFileSystem>> createFileSystemAsync,
            Func<string, InspectionRules> deserialiseRules,
            Func<InspectionRules, IEnumerable<JsonLogicOperatorRegistry>, IFabricFileSystem, Task<IEnumerable<TestResult>>> runInspectionAsync,
            Func<IFabricFileSystem, FabricRemoteFileSystem?> subscribeToProgress,
            Action<FabricRemoteFileSystem?> unsubscribeFromProgress)
        {
            _args = args;
            _credential = credential;
            _onMessage = onMessage;
            _onItemMessage = onItemMessage;
            _onDialogMessage = onDialogMessage;
            _createFileSystemAsync = createFileSystemAsync;
            _deserialiseRules = deserialiseRules;
            _runInspectionAsync = runInspectionAsync;
            _subscribeToProgress = subscribeToProgress;
            _unsubscribeFromProgress = unsubscribeFromProgress;
        }

        public async Task ExecuteAsync(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var outputRootPath = _args.OutputDirPath ?? string.Empty;
            var isOneLakeOutput = OneLakeOutputUploader.IsOneLakeDfsUrl(outputRootPath);
            var localOutputDirPath = outputRootPath;
            var localStagingCreated = false;

            if (isOneLakeOutput)
            {
                localOutputDirPath = Path.Combine(AppUtils.GetTempRootFolderPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(localOutputDirPath);
                localStagingCreated = true;
                _onMessage(MessageTypeEnum.Information, string.Format("Staging output artifacts locally at \"{0}\" before uploading to OneLake.", localOutputDirPath));
            }

            try
            {
                var context = new OutputContext
                {
                    TestResults = testResults,
                    LocalOutputDirPath = localOutputDirPath,
                    IsOneLakeOutput = isOneLakeOutput,
                    TestedFilePath = BuildTestedFilePath(),
                    RulesFilePath = _args.RulesFilePath,
                    Verbose = _args.Verbose,
                    OverwriteOutput = _args.OverwriteOutput,
                    FabricItem = _args.FabricItem,
                    FabricWorkspaceId = _args.FabricWorkspaceId,
                    DeleteOutputDirOnExit = _args.DeleteOutputDirOnExit,
                    CONSOLEOutput = _args.CONSOLEOutput || _args.ADOOutput || _args.GITHUBOutput,
                    OnMessage = _onMessage,
                    OnItemMessage = _onItemMessage,
                    OnDialogMessage = _onDialogMessage,
                };

                var writers = BuildWriters(pageRenderer, registries);

                // Ensure output dir exists for file-based writers
                if (_args.JSONOutput || _args.HTMLOutput || _args.PNGOutput)
                {
                    if (!Directory.Exists(localOutputDirPath))
                    {
                        Directory.CreateDirectory(localOutputDirPath);
                    }
                }

                foreach (var writer in writers)
                {
                    await writer.WriteAsync(context).ConfigureAwait(false);
                }

                if (isOneLakeOutput && context.OutputArtifacts.Any())
                {
                    await UploadOutputArtifactsToOneLakeAsync(outputRootPath, context.OutputArtifacts).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _onMessage(MessageTypeEnum.Error, string.Format("Could not output results. Exception: {0}", e.Message));
            }
            finally
            {
                if (localStagingCreated && Directory.Exists(localOutputDirPath))
                {
                    Directory.Delete(localOutputDirPath, true);
                }
            }
        }

        internal List<IResultOutputWriter> BuildWriters(IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var writers = new List<IResultOutputWriter>();

            if (_args.CONSOLEOutput || _args.ADOOutput || _args.GITHUBOutput)
            {
                writers.Add(new ConsoleResultWriter());
            }

            // JSON must run before HTML (HTML consumes JsonTestRun)
            if (_args.JSONOutput || _args.HTMLOutput)
            {
                writers.Add(new JsonResultWriter());
            }

            if (_args.PNGOutput || _args.HTMLOutput)
            {
                writers.Add(new PngResultWriter(
                    pageRenderer,
                    registries,
                    _createFileSystemAsync,
                    _deserialiseRules,
                    _runInspectionAsync,
                    _subscribeToProgress,
                    _unsubscribeFromProgress));
            }

            if (_args.HTMLOutput)
            {
                writers.Add(new HtmlResultWriter(pageRenderer));
            }

            return writers;
        }

        private string BuildTestedFilePath()
        {
            string path;
            if (!string.IsNullOrWhiteSpace(_args.FabricWorkspaceId))
            {
                path = string.Concat("Workspace: ", _args.FabricWorkspaceId);
                if (!string.IsNullOrWhiteSpace(_args.FabricItem))
                {
                    path = string.Concat(path, " | Item: ", _args.FabricItem);
                }
            }
            else
            {
                path = _args.FabricItem ?? string.Empty;
            }
            return path;
        }

        private async Task UploadOutputArtifactsToOneLakeAsync(
            string outputRootUrl,
            IEnumerable<(string LocalPath, string RelativePath)> artifacts)
        {
            if (_credential == null)
            {
                throw new InvalidOperationException("OneLake output upload requires authentication.");
            }

            foreach (var artifact in artifacts)
            {
                var remoteUrl = OneLakeOutputUploader.CombineUrl(outputRootUrl, artifact.RelativePath);
                await OneLakeOutputUploader.UploadFileAsync(artifact.LocalPath, remoteUrl, false, _credential,
                    onProgress: msg => _onMessage(MessageTypeEnum.Information, msg)).ConfigureAwait(false);
            }
        }
    }
}
