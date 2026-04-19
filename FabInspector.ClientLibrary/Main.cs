using Azure.Core;
using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Exceptions;
using FabInspector.Core.Output;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace FabInspector.ClientLibrary
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

        public static async Task AttendedRun(string fabricWorkspaceId, string fabricItem, string rulesFilePath, string outputPath, bool verbose, bool parallel, bool jsonOutput, bool htmlOutput, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var formatsString = string.Concat(jsonOutput ? "JSON" : string.Empty, ",", htmlOutput ? "HTML" : string.Empty);
            var verboseString = verbose.ToString();
            var parallelString = parallel.ToString();

            string resolvedPbiFilePath = string.Empty;

            string authmethod = "local";

            // Determine presence of auth-related args, if either fabricWorkspaceId is provided or fabricitem is a guid or rulesFilePath is a OneLake DFS URL or outputPath is a OneLake DFS URL, then set authmethod to interactive if no auth method is specified
            if (!string.IsNullOrWhiteSpace(fabricWorkspaceId) || Guid.TryParse(fabricItem, out _) || OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesFilePath) || OneLakeOutputUploader.IsOneLakeDfsUrl(outputPath))
            {
                authmethod = "interactive";
            }

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
                        .DownloadFileToMemoryStreamAsync(rulesPath, _credential,
                            onProgress: msg => OnMessageIssued(MessageTypeEnum.Information, msg))
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
                            args.ClientId!,
                            args.ClientSecret!,
                            args.TenantId!
                        );
                        break;

                    case "certificate":
                        _credential = FabricAuthenticationHelper.CreateCertificateCredential(
                            args.ClientId!,
                            args.TenantId!,
                            args.CertificatePath!,
                            args.CertificatePassword
                        );
                        break;

                    case "federatedtoken":
                        _credential = FabricAuthenticationHelper.CreateFederatedTokenCredential(
                            args.ClientId!,
                            args.FederatedToken!,
                            args.TenantId!
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

            try
            {
                if (!args.Parallel)
                {
                    await RunSingleThreadedAsync(args, pageRenderer, registries).ConfigureAwait(false);
                }
                else
                {
                    await RunParallelAsync(args, pageRenderer, registries).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
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
                _cachedToken = await _credential!.GetTokenAsync(tokenRequestContext, default).ConfigureAwait(false);
                _tokenInitialized = true;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken.Token);
                if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }

                // Expose the authenticated client and credential context for JSON Logic operators (e.g. daxquery)
                FabInspector.Core.Part.ContextService.HttpClient = _httpClient;
                FabInspector.Core.Part.ContextService.Credential = _credential;
                
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        public static async Task RunSingleThreadedAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;
            IEnumerable<TestResult>? testResults = null;
            
            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            SetPartContext();

            var rules = await Task.Run(() => DeserialiseRulesFromPath(args.RulesFilePath ?? string.Empty)).ConfigureAwait(false);
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

            var rules = await Task.Run(() => DeserialiseRulesFromPath(args.RulesFilePath ?? string.Empty)).ConfigureAwait(false);
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
                FabInspector.Core.Part.ContextService.FabricWorkspaceId = _args.FabricWorkspaceId;
                FabInspector.Core.Part.ContextService.FabricItem = _args.FabricItem;
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

        private static async Task<IFabricFileSystem> CreateFileSystemAsync()
        {
            if (!string.IsNullOrWhiteSpace(_args!.FabricWorkspaceId))
            {
                if (Main._credential == null)
                {
                    throw new InvalidOperationException("Authentication credential is required for Fabric workspace access.");
                }

                // Item-scoped vs workspace-scoped mode
                return string.IsNullOrWhiteSpace(_args!.FabricItem)
                    ? new FabricRemoteFileSystem(_args!.FabricWorkspaceId, Main._credential, Main._httpClient)
                    : await FabricRemoteFileSystem.CreateItemScopedAsync(_args!.FabricWorkspaceId, _args!.FabricItem, Main._credential, Main._httpClient).ConfigureAwait(false);
            }

            return new FabricLocalFileSystem(_args!.FabricItem ?? string.Empty);
        }

        private static FabricRemoteFileSystem? SubscribeToProgressEvents(IFabricFileSystem fileSystem)
        {
            if (fileSystem is FabricRemoteFileSystem rfs)
            {
                rfs.ProgressReported += Insp_MessageIssued;
                return rfs;
            }
            return null;
        }

        private static void UnsubscribeFromProgressEvents(FabricRemoteFileSystem? remoteFs)
        {
            if (remoteFs != null)
            {
                remoteFs.ProgressReported -= Insp_MessageIssued;
            }
        }

        private static async Task<IEnumerable<TestResult>> RunInspectionAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem)
        {
            var insp = new Inspector(rules, registries, fileSystem);
            try
            {
                insp.MessageIssued += Insp_MessageIssued;
                return await Task.Run(() => insp.Inspect()).ConfigureAwait(false);
            }
            finally
            {
                insp.MessageIssued -= Insp_MessageIssued;
            }
        }

        private static IEnumerable<TestResult> FilterResults(IEnumerable<TestResult> results)
        {
            return results.Where(_ => _args!.Verbose || !_.Pass);
        }

        private static async Task<IEnumerable<TestResult>?> RunSingleThreadedAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            FabricRemoteFileSystem? remoteFs = null;

            try
            {
                var fileSystem = await CreateFileSystemAsync().ConfigureAwait(false);
                remoteFs = SubscribeToProgressEvents(fileSystem);

                var testResults = await RunInspectionAsync(rules, registries, fileSystem).ConfigureAwait(false);
                return FilterResults(testResults);
            }
            catch (Exception e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            finally
            {
                UnsubscribeFromProgressEvents(remoteFs);
            }

            return Enumerable.Empty<TestResult>();
        }

        private static async Task OutputResultsAsync(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var orchestrator = new ResultOutputOrchestrator(
                _args!,
                _credential,
                OnMessageIssued,
                OnMessageIssued,
                RaiseWinMessage,
                CreateFileSystemAsync,
                DeserialiseRulesFromPath,
                RunInspectionAsync,
                SubscribeToProgressEvents,
                UnsubscribeFromProgressEvents);

            await orchestrator.ExecuteAsync(testResults, pageRenderer, registries).ConfigureAwait(false);
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