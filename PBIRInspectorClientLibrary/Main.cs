using Azure.Core;
using PBIRInspectorClientLibrary.Utils;
using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Exceptions;
using PBIRInspectorLibrary.Output;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace PBIRInspectorClientLibrary
{
    public class Main
    {
        public static event EventHandler<MessageIssuedEventArgs>? WinMessageIssued;
        
        private static Args? _args = null;
        private static int _errorCount = 0;
        private static int _warningCount = 0;
        // Token caching (#1)
        private static readonly HttpClient _httpClient = new HttpClient();
        private static TokenCredential? _credential = null;
        private static AccessToken _cachedToken;
        private static bool _tokenInitialized;
        private static readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);

        public static int ErrorCount
        {
            get
            {
                return _errorCount;
            }
        }

        public static void IncrementErrorCount()
        {
            Interlocked.Increment(ref _errorCount);
        }

        public static int WarningCount
        {
            get
            {
                return _warningCount;
            }
        }

        public static void IncrementWarningCount()
        {
            Interlocked.Increment(ref _warningCount);
        }

        public static async Task Run(string fabricWorkspaceId, string fabricItem, string rulesFilePath, string outputPath, bool verbose, bool parallel, bool jsonOutput, bool htmlOutput, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var formatsString = string.Concat(jsonOutput ? "JSON" : string.Empty, ",", htmlOutput ? "HTML" : string.Empty);
            var verboseString = verbose.ToString();
            var parallelString = parallel.ToString();

            string resolvedPbiFilePath = string.Empty;

            string authmethod = "interactive";

            var args = new Args { FabricWorkspaceId = fabricWorkspaceId, FabricItem = fabricItem, RulesFilePath = rulesFilePath, OutputPath = outputPath, FormatsString = formatsString, VerboseString = verboseString, ParallelString = parallelString, AuthMethod = authmethod };

            await Run(args, pageRenderer, registries);
        }

        public static InspectionRules DeserialiseRulesFromPath(string rulesPath)
        {
            var isOneLakeRulesPath = OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesPath);

            try
            {
                //TODO: consider validating rules file against schema here to provide more specific error message if rules file is invalid. 
                InspectionRules? inspectionRules;
                if (isOneLakeRulesPath)
                {
                    if (_credential == null)
                    {
                        throw new InvalidOperationException(
                            "OneLake rules URL requires authentication. Use -authmethod interactive, clientsecret, certificate, federatedtoken, or managedidentity.");
                    }

                    using var rulesStream = OneLakeRulesFileDownloader
                        .DownloadFileToMemoryStreamAsync(rulesPath, _credential)
                        .GetAwaiter()
                        .GetResult();
                    inspectionRules = JsonUtils.Deserialise<InspectionRules>(rulesStream);
                }
                else
                {
                    inspectionRules = JsonUtils.DeserialiseFromPath<InspectionRules>(rulesPath);
                }

                if (inspectionRules == null || inspectionRules.Rules == null || inspectionRules.Rules.Count == 0)
                {
                    throw new PBIRInspectorException(string.Format("No rule definitions were found within rules file at \"{0}\".", rulesPath));
                }
                else
                {
                    return inspectionRules;
                }
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PBIRInspectorException(string.Format("Rules file with path \"{0}\" not found.", rulesPath), e);
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new PBIRInspectorException(string.Format("Could not deserialise rules file with path \"{0}\". Check that the file is valid json and following the correct schema for Fab Inspector rules.", rulesPath), e);
            }
            catch (InvalidOperationException e) when (isOneLakeRulesPath)
            {
                throw new PBIRInspectorException(string.Format("Could not load rules file from OneLake URL \"{0}\".", rulesPath), e);
            }
        }

        public static async Task Run(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            // Authenticate based on auth method
            if (args.AuthMethod != "local")
            {
                switch (args.AuthMethod.ToLower())
                {
                    case "interactive":
                        _credential = FabricAuthenticationHelper.CreateInteractiveCredential(
                            args.ClientId,
                            args.TenantId
                        );
                        break;

                    case "clientsecret":
                        _credential = FabricAuthenticationHelper.CreateClientSecretCredential(
                            args.ClientId,
                            args.ClientSecret,
                            args.TenantId
                        );
                        break;

                    case "certificate":
                        _credential = FabricAuthenticationHelper.CreateCertificateCredential(
                            args.ClientId,
                            args.TenantId,
                            args.CertificatePath,
                            args.CertificatePassword
                        );
                        break;

                    case "federatedtoken":
                        _credential = FabricAuthenticationHelper.CreateFederatedTokenCredential(
                            args.ClientId,
                            args.FederatedToken,
                            args.TenantId
                        );
                        break;

                    case "managedidentity":
                        _credential = FabricAuthenticationHelper.CreateManagedIdentityCredential(
                            args.ClientId
                        );
                        break;

                    default:
                        throw new ArgumentException($"Unsupported authentication method: {args.AuthMethod}");
                }

                await Main.EnsureAuthenticatedAsync();
            }

            if (!args.Parallel)
            {
                await RunSingleThreadedAsync(args, pageRenderer, registries).ConfigureAwait(false);
            }
            else
            {
                await RunParallelAsync(args, pageRenderer, registries).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets an access token from the credential and configures HTTP client authorization.
        /// Caches token and only refreshes when within 5 minutes of expiry (#1).
        /// </summary>
        /// TODO: EnsureLogout is called on credential when test run is complete to clear cached token and any associated authentication state.
        private static async Task EnsureAuthenticatedAsync()
        {
            // Fast path: token is valid with 5-minute buffer
            if (_tokenInitialized && _cachedToken.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return;
            }

            await _tokenSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_tokenInitialized && _cachedToken.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    return;
                }

                var tokenRequestContext = new TokenRequestContext(AuthenticationHelper.FabricScopes);
                _cachedToken = await _credential.GetTokenAsync(tokenRequestContext, default).ConfigureAwait(false);
                _tokenInitialized = true;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken.Token);
                if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }

                // Expose the authenticated client and credential context for JSON Logic operators (e.g. daxquery)
                PBIRInspectorLibrary.Part.ContextService.HttpClient = _httpClient;
                PBIRInspectorLibrary.Part.ContextService.Credential = _credential;
                
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        public static async Task RunSingleThreadedAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;
            IEnumerable<TestResult> testResults = null;
            
            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            SetPartContext();

            var rules = await Task.Run(() => DeserialiseRulesFromPath(Main._args.RulesFilePath)).ConfigureAwait(false);
            testResults = await RunSingleThreadedAsync(rules, registries).ConfigureAwait(false);

            if (testResults != null && testResults.Any())
            {
                await OutputResultsAsync(testResults, pageRenderer, registries).ConfigureAwait(false);
            }
            else
            {
                OnMessageIssued(MessageTypeEnum.Information, "No test results found.");
            }
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        public static async Task RunParallelAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            SetPartContext();

            var rules = await Task.Run(() => DeserialiseRulesFromPath(Main._args.RulesFilePath)).ConfigureAwait(false);
            var ruleBuckets = ChunkInspectionRules(rules);
            var globalResults = new ConcurrentBag<TestResult>();

            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Parallel test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            var tasks = ruleBuckets.Select(async bucket => await RunSingleThreadedAsync(bucket, registries).ConfigureAwait(false));
            var allResults = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var localResults in allResults)
            {
                foreach (var result in localResults ?? Enumerable.Empty<TestResult>())
                {
                    globalResults.Add(result);
                }
            }

            await OutputResultsAsync(globalResults.ToList().OrderBy(_ => _.RuleId), pageRenderer, registries).ConfigureAwait(false);
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        private static void SetPartContext()
        {
            if (_args != null)
            {
                PBIRInspectorLibrary.Part.ContextService.FabricWorkspaceId = _args.FabricWorkspaceId;
                PBIRInspectorLibrary.Part.ContextService.FabricItem = _args.FabricItem;
            }
        }

        private static List<InspectionRules> ChunkInspectionRules(InspectionRules rules)
        {
            var processorCount = Environment.ProcessorCount;
            var allRules = rules.Rules;
            int totalRules = allRules.Count;
            int chunkSize = (int)Math.Ceiling((double)totalRules / processorCount);

            var ruleBuckets = allRules
                .Select((rule, index) => new { rule, index })
                .GroupBy(x => x.index / chunkSize)
                .Select(g => new InspectionRules { Rules = g.Select(x => x.rule).ToList() })
                .ToList();

            return ruleBuckets;
        }

        private static async Task<IEnumerable<TestResult>?> RunSingleThreadedAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            Inspector? insp = null;

            try
            {
                // Determine which file system to use based on Fabric workspace configuration
                IFabricFileSystem fileSystem;
                if (!string.IsNullOrWhiteSpace(Main._args.FabricWorkspaceId))
                {
                    if (Main._credential == null)
                    {
                        throw new InvalidOperationException("Authentication credential is required for Fabric workspace access.");
                    }

                    // Item-scoped vs workspace-scoped mode
                    fileSystem = string.IsNullOrWhiteSpace(Main._args.FabricItem)
                        ? new FabricRemoteFileSystem(Main._args.FabricWorkspaceId, Main._credential, Main._httpClient)
                        : await FabricRemoteFileSystem.CreateItemScopedAsync(Main._args.FabricWorkspaceId, Main._args.FabricItem, Main._credential, Main._httpClient).ConfigureAwait(false);
                }
                else
                {
                    // Use PhysicalFileSystem with the specified path
                    fileSystem = new FabricLocalFileSystem(Main._args.FabricItem ?? string.Empty);
                }
                
                insp = new Inspector(rules, registries, fileSystem);

                insp.MessageIssued += Insp_MessageIssued;
                var testResults = await Task.Run(() => insp.Inspect()).ConfigureAwait(false);
                return testResults.Where(_ => (!Main._args.Verbose && !_.Pass) || (Main._args.Verbose));
            }
            catch (PBIRInspectorException e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            catch (ArgumentException e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            catch (Exception e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            finally
            {
                
                if (insp != null)
                {
                    insp.MessageIssued -= Insp_MessageIssued;
                }
            }

            // Ensure all code paths return a value
            return Enumerable.Empty<TestResult>();
        }

        private static async Task OutputResultsAsync(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            string jsonTestRun = string.Empty;
            Inspector? fieldMapInsp = null;
            IEnumerable<TestResult> fieldMapResults = null;
            var outputArtifacts = new List<(string LocalPath, string RelativePath)>();

            var outputRootPath = Main._args.OutputDirPath ?? string.Empty;
            var isOneLakeOutput = OneLakeOutputUploader.IsOneLakeDfsUrl(outputRootPath);
            var localOutputDirPath = outputRootPath;
            var localStagingCreated = false;

            if (isOneLakeOutput)
            {
                localOutputDirPath = Path.Combine(AppUtils.GetTempRootFolderPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(localOutputDirPath);
                localStagingCreated = true;
                OnMessageIssued(MessageTypeEnum.Information, string.Format("Staging output artifacts locally at \"{0}\" before uploading to OneLake.", localOutputDirPath));
            }

            try
            {
                if (Main._args.CONSOLEOutput || Main._args.ADOOutput || Main._args.GITHUBOutput)
                {
                    foreach (var result in testResults)
                    {
                        //TODO: use Test log type json property instead
                        var msgType = result.Pass ? MessageTypeEnum.Information : result.LogType;
                        OnMessageIssued(result.ItemPath, msgType, result.Message);
                    }

                    // Summarise error and warning counts
                    if (testResults != null && testResults.Any())
                    {
                        OnMessageIssued(MessageTypeEnum.Information, string.Format("Test run summary: {0} errors, {1} warnings.",
                            testResults.Count(_ => _.LogType == MessageTypeEnum.Error),
                            testResults.Count(_ => _.LogType == MessageTypeEnum.Warning)));
                    }
                    else
                    {
                        OnMessageIssued(MessageTypeEnum.Information, "Test run summary: No test results found.");
                    }
                }

                //Ensure output dir exists
                if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.JSONOutput || Main._args.HTMLOutput || Main._args.PNGOutput))
                {
                    if (!Directory.Exists(localOutputDirPath))
                    {
                        Directory.CreateDirectory(localOutputDirPath);
                    }
                }

                if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.JSONOutput || Main._args.HTMLOutput))
                {
                    var outputFilePath = string.Empty;
                    var outputFileIdentifier = !string.IsNullOrWhiteSpace(Main._args.FabricItem) ? Path.GetFileNameWithoutExtension(Main._args.FabricItem) :  Main._args.FabricWorkspaceId;
                    var timestampSuffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var jsonFileName = isOneLakeOutput
                        ? string.Concat("TestRun_", outputFileIdentifier, "_", timestampSuffix, ".json")
                        : string.Concat("TestRun_", outputFileIdentifier, ".json");

                    if (!string.IsNullOrEmpty(localOutputDirPath))
                    {
                        outputFilePath = Path.Combine(localOutputDirPath, jsonFileName);
                    }
                    else
                    {
                        throw new ArgumentException("Directory with path \"{0}\" does not exist", localOutputDirPath);
                    }

                    var testedFilePath = BuildTestedFilePath();

                    var testRun = new TestRun() { CompletionTime = DateTime.Now, TestedFilePath = testedFilePath, RulesFilePath = Main._args.RulesFilePath, Verbose = Main._args.Verbose, Results = testResults };
                    jsonTestRun = JsonSerializer.Serialize(testRun);
                    if (Main._args.JSONOutput)
                    {
                        OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing JSON output to file at \"{0}\".", outputFilePath));
                        File.WriteAllText(outputFilePath, jsonTestRun, System.Text.Encoding.UTF8);

                        if (isOneLakeOutput)
                        {
                            outputArtifacts.Add((outputFilePath, jsonFileName));
                        }
                    }
                }

                if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.PNGOutput || Main._args.HTMLOutput))
                {
                    //optimisation - run only for report-related rules
                    if (testResults.Any(_ => _.RuleItemType.Contains("Report", StringComparison.InvariantCultureIgnoreCase) ||
                    _.RuleItemType.Contains("report_deprecated", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Determine which file system to use based on Fabric workspace configuration
                        IFabricFileSystem fieldMapFileSystem;
                        if (!string.IsNullOrWhiteSpace(Main._args.FabricWorkspaceId))
                        {
                            if (Main._credential == null)
                            {
                                throw new InvalidOperationException("Authentication credential is required for Fabric workspace access.");
                            }

                            // Item-scoped vs workspace-scoped mode
                            //TODO: reuse filesystem object from test run
                            fieldMapFileSystem = string.IsNullOrWhiteSpace(Main._args.FabricItem)
                                ? new FabricRemoteFileSystem(Main._args.FabricWorkspaceId, Main._credential, Main._httpClient)
                                : await FabricRemoteFileSystem.CreateItemScopedAsync(Main._args.FabricWorkspaceId, Main._args.FabricItem, Main._credential, Main._httpClient).ConfigureAwait(false);
                        }
                        else
                        {
                            // Use PhysicalFileSystem with the specified path
                            fieldMapFileSystem = new FabricLocalFileSystem(Main._args.FabricItem ?? string.Empty);
                        }
                        // Create file system for field map inspection
                        //IFabricFileSystem fieldMapFileSystem = new FabricLocalFileSystem(Main._args.FabricItem ?? string.Empty);
                        var fieldMapPathRules = DeserialiseRulesFromPath(Constants.ReportPageFieldMapFilePath);
                        fieldMapInsp = new Inspector(fieldMapPathRules, registries, fieldMapFileSystem);

                        fieldMapResults = await Task.Run(() => fieldMapInsp.Inspect()).ConfigureAwait(false);

                        var outputPNGDirPath = Path.Combine(localOutputDirPath, Constants.PNGOutputDir);

                        if (Directory.Exists(outputPNGDirPath))
                        {
                            if (Main._args.OverwriteOutput)
                            {
                                Directory.Delete(outputPNGDirPath, true);
                            }
                            else
                            {
                                if (isOneLakeOutput)
                                {
                                    throw new PBIRInspectorException(string.Format("Output directory already exists at \"{0}\" and overwriteoutput is false.", outputPNGDirPath));
                                }

                                //If the directory already exists and overwrite is not set, ask user if they want to delete existing content.
                                var eventArgs = RaiseWinMessage(MessageTypeEnum.Dialog, string.Format("Directory already exists at \"{0}\". Do you want to overwrite existing content?", outputPNGDirPath));
                                if (eventArgs.DialogOKResponse)
                                {
                                    Directory.Delete(outputPNGDirPath, true);
                                }
                                else
                                {
                                    OnMessageIssued(MessageTypeEnum.Information, "Skipping PNG output as directory already exists and overwrite not set.");
                                    return;
                                }
                            }
                        }

                        Directory.CreateDirectory(outputPNGDirPath);
                        OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing report page wireframe images to files at \"{0}\".", outputPNGDirPath));
                        pageRenderer.DrawReportPages(fieldMapResults, testResults, outputPNGDirPath);

                        if (isOneLakeOutput)
                        {
                            foreach (var pngPath in Directory.GetFiles(outputPNGDirPath, "*.png", SearchOption.TopDirectoryOnly))
                            {
                                var relativePngPath = Path.Combine(Constants.PNGOutputDir, Path.GetFileName(pngPath));
                                outputArtifacts.Add((pngPath, relativePngPath));
                            }
                        }
                    }
                }

                if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && Main._args.HTMLOutput)
                {
                    string pbiinspectorlogobase64 = string.Concat(Constants.Base64ImgPrefix, pageRenderer.ConvertBitmapToBase64(Constants.PBIInspectorPNG));
                    //string nowireframebase64 = string.Concat(Base64ImgPrefix, ImageUtils.ConvertBitmapToBase64(@"Files\png\nowireframe.png"));
                    string template = File.ReadAllText(Constants.TestRunHTMLTemplate);
                    string html = template.Replace(Constants.LogoPlaceholder, pbiinspectorlogobase64, StringComparison.OrdinalIgnoreCase);
                    html = html.Replace(Constants.VersionPlaceholder, AppUtils.About(), StringComparison.OrdinalIgnoreCase);
                    html = html.Replace(Constants.JsonPlaceholder, jsonTestRun, StringComparison.OrdinalIgnoreCase);

                    var outputHTMLFilePath = Path.Combine(localOutputDirPath, Constants.TestRunHTMLFileName);

                    OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing HTML output to file at \"{0}\".", outputHTMLFilePath));
                    File.WriteAllText(outputHTMLFilePath, html);

                    if (isOneLakeOutput)
                    {
                        outputArtifacts.Add((outputHTMLFilePath, Constants.TestRunHTMLFileName));
                    }

                    //Results have been written to a temporary directory so show output to user automatically.
                    if (!isOneLakeOutput && Main._args.DeleteOutputDirOnExit && !Main._args.CONSOLEOutput)
                    {
                        AppUtils.OpenUrl(outputHTMLFilePath);
                    }
                }

                if (isOneLakeOutput && outputArtifacts.Any())
                {
                    await UploadOutputArtifactsToOneLakeAsync(outputRootPath, outputArtifacts, Main._args.OverwriteOutput).ConfigureAwait(false);
                }
            }
            finally
            {
                if (localStagingCreated && Directory.Exists(localOutputDirPath))
                {
                    Directory.Delete(localOutputDirPath, true);
                }
            }
        }

        private static string BuildTestedFilePath()
        {
            string path;
            if (!string.IsNullOrWhiteSpace(Main._args.FabricWorkspaceId))
            {
                path = string.Concat("Workspace: ", Main._args.FabricWorkspaceId);
                if (!string.IsNullOrWhiteSpace(Main._args.FabricItem))
                {
                    path = string.Concat(path, " | Item: ", Main._args.FabricItem);
                }
            }
            else
            {
                path = Main._args.FabricItem;
            }
            return path;
        }

        private static async Task UploadOutputArtifactsToOneLakeAsync(
            string outputRootUrl,
            IEnumerable<(string LocalPath, string RelativePath)> artifacts,
            bool overwrite)
        {
            if (_credential == null)
            {
                throw new InvalidOperationException("OneLake output upload requires authentication.");
            }

            foreach (var artifact in artifacts)
            {
                var remoteUrl = OneLakeOutputUploader.CombineUrl(outputRootUrl, artifact.RelativePath);
                if (!overwrite)
                {
                    var exists = await OneLakeOutputUploader.FileExistsAsync(remoteUrl, _credential).ConfigureAwait(false);
                    if (exists)
                    {
                        throw new PBIRInspectorException(
                            $"Output artifact already exists at '{remoteUrl}'. Set -overwriteoutput true to overwrite.");
                    }
                }
            }

            foreach (var artifact in artifacts)
            {
                var remoteUrl = OneLakeOutputUploader.CombineUrl(outputRootUrl, artifact.RelativePath);
                OnMessageIssued(MessageTypeEnum.Information, string.Format("Uploading output artifact to OneLake at \"{0}\".", remoteUrl));
                await OneLakeOutputUploader.UploadFileAsync(artifact.LocalPath, remoteUrl, overwrite, _credential).ConfigureAwait(false);
            }
        }

        public static void CleanUpTestRunTempFolder()
        {
            if (_args != null && _args.DeleteOutputDirOnExit && Directory.Exists(_args.OutputDirPath))
            {
                Directory.Delete(_args.OutputDirPath, true);
            }
        }

        public static void CleanUpRootTempFolder()
        {
            if (!Directory.Exists(AppUtils.GetTempRootFolderPath()))
            {
                return;
            }

            var tempRootDir = AppUtils.GetTempRootFolderPath();  
            Directory.Delete(tempRootDir, true);
        }

        private static void Insp_MessageIssued(object? sender, MessageIssuedEventArgs e)
        {
            MessageIssued(e);
        }

        private static MessageIssuedEventArgs RaiseWinMessage(MessageTypeEnum messageType, string message)
        {
            var args = new MessageIssuedEventArgs(message, messageType);
            WinMessageIssued?.Invoke(null, args);
            return args;
        }

        private static void OnMessageIssued(MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(message, messageType);
            MessageIssued(e);
        }

        private static void OnMessageIssued(string itemPath, MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(itemPath, message, messageType);
            MessageIssued(e);
        }

        private static void MessageIssued(MessageIssuedEventArgs e)
        {
            if (_args != null && (_args.ADOOutput || _args.GITHUBOutput))
            {
                if (e.MessageType == MessageTypeEnum.Error) IncrementErrorCount();
                if (e.MessageType == MessageTypeEnum.Warning) IncrementWarningCount();
            }

            EventHandler<MessageIssuedEventArgs>? handler = WinMessageIssued;
            if (handler != null)
            {
                handler(null, e);
            }
        }
    }
}