using Azure.Core;
using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Exceptions;
using FabInspector.Core.Inspection;
using FabInspector.Core.Output;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace FabInspector.ClientLibrary
{
    public class Main
    {
        public static event EventHandler<MessageIssuedEventArgs>? WinMessageIssued;
        
        private static Args? _args = null;
        private static int _errorCount = 0;
        private static int _warningCount = 0;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static ITokenProvider? _tokenProvider = null;

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
                    if (_tokenProvider == null)
                    {
                        throw new InvalidOperationException(
                            "OneLake rules URL requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
                    }

                    using var rulesStream = OneLakeRulesFileDownloader
                        .DownloadFileToMemoryStreamAsync(rulesPath, _tokenProvider.Credential,
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

        /// <summary>
        /// Runs inspection and returns structured results without writing files.
        /// Intended for programmatic consumers such as the MCP server mode.
        /// </summary>
        public static async Task<TestRun> RunAndReturnResultsAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            // Authenticate if needed (same logic as Run)
            if (args.AuthMethod != "local")
            {
                await AuthenticateAsync(args).ConfigureAwait(false);
            }

            SetPartContext();

            var testResults = await ExecuteRuleSetsAsync(args, registries).ConfigureAwait(false);

            return new TestRun
            {
                CompletionTime = DateTime.UtcNow,
                TestedFilePath = args.FabricItem,
                RulesFilePath = args.RulesFilePath,
                RulesCatalogPath = args.RulesCatalogPath,
                Verbose = args.Verbose,
                Results = testResults
            };
        }

        /// <summary>
        /// Overload that accepts a pre-built <see cref="ITokenProvider"/> (e.g. a delegated
        /// per-user token provider from a Blazor / ASP.NET host). Skips the
        /// <see cref="FabricAuthenticationHelper"/>-based credential construction entirely
        /// so the caller controls token lifetime and audience.
        /// The caller is responsible for opening a <c>ContextService.BeginScope(...)</c>
        /// around this call to isolate per-user ambient state on concurrent flows.
        /// </summary>
        public static async Task<TestRun> RunAndReturnResultsAsync(Args args, ITokenProvider tokenProvider, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            if (tokenProvider == null) throw new ArgumentNullException(nameof(tokenProvider));

            _args = args;
            _tokenProvider = tokenProvider;
            FabInspector.Core.Part.ContextService.HttpClient = _httpClient;
            FabInspector.Core.Part.ContextService.TokenProvider = tokenProvider;

            SetPartContext();

            var testResults = await ExecuteRuleSetsAsync(args, registries).ConfigureAwait(false);

            return new TestRun
            {
                CompletionTime = DateTime.UtcNow,
                TestedFilePath = args.FabricItem,
                RulesFilePath = args.RulesFilePath,
                RulesCatalogPath = args.RulesCatalogPath,
                Verbose = args.Verbose,
                Results = testResults
            };
        }

        private static async Task AuthenticateAsync(Args args)
        {
            TokenCredential credential;
            switch (args.AuthMethod.ToLower())
            {
                case "interactive":
                    credential = FabricAuthenticationHelper.CreateInteractiveCredential(args.ClientId, args.TenantId);
                    break;
                case "clientsecret":
                    credential = FabricAuthenticationHelper.CreateClientSecretCredential(args.ClientId!, args.ClientSecret!, args.TenantId!);
                    break;
                case "certificate":
                    credential = FabricAuthenticationHelper.CreateCertificateCredential(args.ClientId!, args.TenantId!, args.CertificatePath!, args.CertificatePassword);
                    break;
                case "federatedtoken":
                    credential = FabricAuthenticationHelper.CreateFederatedTokenCredential(args.ClientId!, args.FederatedToken!, args.TenantId!);
                    break;
                case "managedidentity":
                    credential = FabricAuthenticationHelper.CreateManagedIdentityCredential(args.ClientId);
                    break;
                case "azurecli":
                    credential = FabricAuthenticationHelper.CreateAzureCliCredential(args.TenantId);
                    break;
                default:
                    throw new ArgumentException($"Unsupported authentication method: {args.AuthMethod}");
            }

            _tokenProvider = new CachingTokenProvider(credential);
            await _tokenProvider.GetTokenAsync(AuthenticationHelper.FabricScopes).ConfigureAwait(false);
            FabInspector.Core.Part.ContextService.HttpClient = _httpClient;
            FabInspector.Core.Part.ContextService.TokenProvider = _tokenProvider;
        }

        /// <summary>
        /// Core inspection runner that creates a file system and runs inspection without output.
        /// Shared between RunSingleThreadedAsync and RunAndReturnResultsAsync.
        /// </summary>
        private static async Task<IEnumerable<TestResult>?> RunSingleThreadedCoreAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries)
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

        public static async Task Run(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            // Authenticate based on auth method
            if (args.AuthMethod != "local")
            {
                await AuthenticateAsync(args).ConfigureAwait(false);
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

        public static async Task RunSingleThreadedAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;
            
            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            SetPartContext();

            var testResults = await ExecuteRuleSetsAsync(args, registries).ConfigureAwait(false);

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

            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Parallel test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            SetPartContext();

            var testResults = await ExecuteRuleSetsAsync(args, registries).ConfigureAwait(false);
            await OutputResultsAsync(testResults, pageRenderer, registries).ConfigureAwait(false);
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        private static async Task<IEnumerable<TestResult>> ExecuteRuleSetsAsync(Args args, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            // Push the ambient InspectionContext for the duration of this run so that
            // operators (apiget, daxquery, dfsget, scannerapi, sqlquery) can resolve
            // the HttpClient, TokenProvider, and Fabric workspace/item via
            // InspectionContextHolder.Current. The ContextService.* statics set by
            // SetPartContext/AuthenticateAsync remain in place during the P1
            // transition for any unmigrated callers; they're removed in Phase 5.
            var inspectionContext = new InspectionContext
            {
                HttpClient = _httpClient,
                FabricWorkspaceId = args.FabricWorkspaceId ?? string.Empty,
                FabricItem = args.FabricItem,
                TokenProvider = _tokenProvider
                    ?? throw new InvalidOperationException("Token provider has not been configured. Call AuthenticateAsync or use the ITokenProvider overload of RunAndReturnResultsAsync before executing rules.")
            };

            using var holderScope = InspectionContextHolder.PushScope(inspectionContext);

            var resolvedRuleSets = await ResolveRuleSetsAsync(args).ConfigureAwait(false);
            var combinedResults = new List<TestResult>();

            foreach (var ruleSet in resolvedRuleSets)
            {
                try
                {
                    OnMessageIssued(MessageTypeEnum.Information, $"Running ruleset '{ruleSet.Name}' from '{ruleSet.SourcePath}'.");
                    var currentResults = await ExecuteSingleRuleSetAsync(ruleSet, registries, args.Parallel).ConfigureAwait(false);
                    combinedResults.AddRange(currentResults);
                }
                catch (Exception ex) when (!string.IsNullOrWhiteSpace(args.RulesCatalogPath))
                {
                    OnMessageIssued(MessageTypeEnum.Error, $"Failed to execute ruleset '{ruleSet.Name}' from '{ruleSet.SourcePath}'. {ex.Message}");
                }
            }

            return combinedResults;
        }

        private static async Task<IReadOnlyList<ResolvedRuleSet>> ResolveRuleSetsAsync(Args args)
        {
            if (!string.IsNullOrWhiteSpace(args.RulesCatalogPath))
            {
                var reader = new RulesCatalogReader(_tokenProvider, _httpClient,
                    onProgress: msg => OnMessageIssued(MessageTypeEnum.Information, msg));
                return await reader.ReadResolvedRuleSetsAsync(args.RulesCatalogPath).ConfigureAwait(false);
            }

            var rules = await Task.Run(() => DeserialiseRulesFromPath(args.RulesFilePath ?? string.Empty)).ConfigureAwait(false);
            return new List<ResolvedRuleSet>
            {
                new ResolvedRuleSet
                {
                    Name = "Rules",
                    SourcePath = args.RulesFilePath ?? string.Empty,
                    Rules = rules
                }
            };
        }

        private static async Task<IEnumerable<TestResult>> ExecuteSingleRuleSetAsync(ResolvedRuleSet ruleSet, IEnumerable<JsonLogicOperatorRegistry> registries, bool parallel)
        {
            IEnumerable<TestResult> results;

            if (parallel)
            {
                var ruleBuckets = ChunkInspectionRules(ruleSet.Rules);
                var tasks = ruleBuckets.Select(async bucket => await RunSingleThreadedAsync(bucket, registries).ConfigureAwait(false));
                var allResults = await Task.WhenAll(tasks).ConfigureAwait(false);
                results = allResults.SelectMany(r => r ?? Enumerable.Empty<TestResult>());
            }
            else
            {
                results = await RunSingleThreadedAsync(ruleSet.Rules, registries).ConfigureAwait(false) ?? Enumerable.Empty<TestResult>();
            }

            return results.Select(result =>
            {
                result.RuleSetName = ruleSet.Name;
                result.RuleSetPath = ruleSet.SourcePath;
                return result;
            });
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
                if (Main._tokenProvider == null)
                {
                    throw new InvalidOperationException("Authentication credential is required for Fabric workspace access.");
                }

                // Item-scoped vs workspace-scoped mode
                return string.IsNullOrWhiteSpace(_args!.FabricItem)
                    ? new FabricRemoteFileSystem(_args!.FabricWorkspaceId, Main._tokenProvider, Main._httpClient)
                    : await FabricRemoteFileSystem.CreateItemScopedAsync(_args!.FabricWorkspaceId, _args!.FabricItem, Main._tokenProvider, Main._httpClient).ConfigureAwait(false);
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
            return await RunSingleThreadedCoreAsync(rules, registries).ConfigureAwait(false);
        }

        private static async Task OutputResultsAsync(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var orchestrator = new ResultOutputOrchestrator(
                _args!,
                _tokenProvider?.Credential,
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