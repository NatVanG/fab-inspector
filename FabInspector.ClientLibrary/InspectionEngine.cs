using Azure.Core;
using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Inspection;
using FabInspector.Core.Output;
using System.Net.Http;

namespace FabInspector.ClientLibrary
{
    /// <summary>
    /// Per-run inspection engine. Owns all state that must not bleed across
    /// concurrent inspection runs (args, token provider, error/warning counters,
    /// and the per-run <see cref="MessageIssued"/> event stream).
    ///
    /// Constructed fresh per call by the <see cref="Main"/> static facade (Phase 3
    /// of the DI refactor). Hosts that need true concurrency (e.g. the Blazor
    /// <c>InspectionRunner</c>) can subscribe to a per-instance
    /// <see cref="MessageIssued"/> event and skip the process-wide static event
    /// on <see cref="Main"/> entirely.
    /// </summary>
    public sealed class InspectionEngine
    {
        /// <summary>
        /// Per-instance progress stream. Subscribers receive every message
        /// raised during this engine's run. Use this instead of the static
        /// <see cref="Main.WinMessageIssued"/> event when you need
        /// caller-isolated progress for concurrent inspections.
        /// </summary>
        public event EventHandler<MessageIssuedEventArgs>? MessageIssued;

        private Args? _args;
        private int _errorCount;
        private int _warningCount;
        private readonly HttpClient _httpClient;
        private ITokenProvider? _tokenProvider;

        public int ErrorCount => _errorCount;
        public int WarningCount => _warningCount;
        public ITokenProvider? TokenProvider => _tokenProvider;
        public HttpClient HttpClient => _httpClient;

        public InspectionEngine() : this(new HttpClient()) { }

        public InspectionEngine(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// No-op token provider used when no authentication has been configured
        /// (e.g. fully local CLI / WinForm runs against on-disk files where
        /// <c>AuthMethod == "local"</c>). Local-only operators never call into
        /// this provider; remote operators (apiget, daxquery, dfsget, scannerapi,
        /// sqlquery) will fail with a clear message at token-acquisition time.
        /// </summary>
        private sealed class NullTokenProvider : ITokenProvider
        {
            public static readonly NullTokenProvider Instance = new();

            public Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("This operator requires authentication, but the inspection run was started without a token provider (AuthMethod=\"local\" or no credential supplied).");

            public TokenCredential Credential
                => throw new InvalidOperationException("This operator requires a TokenCredential, but the inspection run was started without a token provider (AuthMethod=\"local\" or no credential supplied).");
        }

        public void IncrementErrorCount() => Interlocked.Increment(ref _errorCount);
        public void IncrementWarningCount() => Interlocked.Increment(ref _warningCount);

        public async Task RunAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

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

        public async Task RunSingleThreadedAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
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

        public async Task RunParallelAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Parallel test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            SetPartContext();

            var testResults = await ExecuteRuleSetsAsync(args, registries).ConfigureAwait(false);
            await OutputResultsAsync(testResults, pageRenderer, registries).ConfigureAwait(false);
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        /// <summary>
        /// Runs inspection and returns structured results without writing files.
        /// Intended for programmatic consumers such as the MCP server mode.
        /// </summary>
        public async Task<TestRun> RunAndReturnResultsAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

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
        /// </summary>
        public async Task<TestRun> RunAndReturnResultsAsync(Args args, ITokenProvider tokenProvider, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            if (tokenProvider == null) throw new ArgumentNullException(nameof(tokenProvider));

            _args = args;
            _tokenProvider = tokenProvider;

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

        private async Task AuthenticateAsync(Args args)
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
        }

        private async Task<IEnumerable<TestResult>?> RunSingleThreadedCoreAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem)
        {
            try
            {
                var testResults = await RunInspectionAsync(rules, registries, fileSystem).ConfigureAwait(false);
                return FilterResults(testResults);
            }
            catch (Exception e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }

            return Enumerable.Empty<TestResult>();
        }

        private async Task<IEnumerable<TestResult>> ExecuteRuleSetsAsync(Args args, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            // Push the ambient InspectionContext for the duration of this run so that
            // operators (apiget, daxquery, dfsget, scannerapi, sqlquery, part, partinfo)
            // can resolve the HttpClient, TokenProvider, and Fabric workspace/item via
            // InspectionContextHolder.Current.
            //
            // For local-only runs (AuthMethod == "local", no Fabric workspace),
            // AuthenticateAsync is skipped so _tokenProvider is null. Substitute
            // NullTokenProvider.Instance: it only throws if a remote operator
            // actually tries to acquire a token, leaving file-based operators free
            // to run without authentication.
            var inspectionContext = new InspectionContext
            {
                HttpClient = _httpClient,
                FabricWorkspaceId = args.FabricWorkspaceId ?? string.Empty,
                FabricItem = args.FabricItem,
                TokenProvider = _tokenProvider ?? NullTokenProvider.Instance
            };

            using var holderScope = InspectionContextHolder.PushScope(inspectionContext);

            var resolvedRuleSets = await ResolveRuleSetsAsync(args).ConfigureAwait(false);
            var fileSystem = await CreateFileSystemAsync().ConfigureAwait(false);
            fileSystem.ScopedItemTypes = GetScopedItemTypes(resolvedRuleSets);
            var remoteFs = SubscribeToProgressEvents(fileSystem);
            var combinedResults = new List<TestResult>();

            try
            {
                foreach (var ruleSet in resolvedRuleSets)
                {
                    try
                    {
                        OnMessageIssued(MessageTypeEnum.Information, $"Running ruleset '{ruleSet.Name}' from '{ruleSet.SourcePath}'.");
                        var currentResults = await ExecuteSingleRuleSetAsync(ruleSet, registries, args.Parallel, fileSystem).ConfigureAwait(false);
                        combinedResults.AddRange(currentResults);
                    }
                    catch (Exception ex) when (!string.IsNullOrWhiteSpace(args.RulesCatalogPath))
                    {
                        OnMessageIssued(MessageTypeEnum.Error, $"Failed to execute ruleset '{ruleSet.Name}' from '{ruleSet.SourcePath}'. {ex.Message}");
                    }
                }
            }
            finally
            {
                UnsubscribeFromProgressEvents(remoteFs);
            }

            return combinedResults;
        }

        internal static IEnumerable<string>? GetScopedItemTypes(IEnumerable<ResolvedRuleSet> resolvedRuleSets)
        {
            var scopedItemTypes = resolvedRuleSets
                .SelectMany(ruleSet => ruleSet.Rules.Rules)
                .SelectMany(rule => (rule.ItemType ?? string.Empty)
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(itemType => !string.IsNullOrWhiteSpace(itemType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return scopedItemTypes.Count == 0 ? null : scopedItemTypes;
        }

        private async Task<IReadOnlyList<ResolvedRuleSet>> ResolveRuleSetsAsync(Args args)
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

        private async Task<IEnumerable<TestResult>> ExecuteSingleRuleSetAsync(ResolvedRuleSet ruleSet, IEnumerable<JsonLogicOperatorRegistry> registries, bool parallel, IFabricFileSystem fileSystem)
        {
            IEnumerable<TestResult> results;

            if (parallel)
            {
                // The ambient InspectionContext was pushed once by ExecuteRuleSetsAsync.
                // Inspector.RunRules mutates per-rule fields (RuleName, Part, PartQuery,
                // ItemPath, MessageReporter) and Inspector.Inspect mutates FabricItem
                // during workspace iteration. Running multiple buckets against a single
                // shared instance races on those slots and produces inconsistent results.
                // Push a per-task clone so each parallel flow's AsyncLocal slot owns its
                // own mutable context.
                var ambient = InspectionContextHolder.Require("parallel ruleset execution");
                var ruleBuckets = ChunkInspectionRules(ruleSet.Rules);
                var tasks = ruleBuckets.Select(async bucket =>
                {
                    var perTaskContext = ambient with { };
                    using var perTaskScope = InspectionContextHolder.PushScope(perTaskContext);
                    return await RunSingleThreadedAsync(bucket, registries, fileSystem).ConfigureAwait(false);
                });
                var allResults = await Task.WhenAll(tasks).ConfigureAwait(false);
                results = allResults.SelectMany(r => r ?? Enumerable.Empty<TestResult>());
            }
            else
            {
                results = await RunSingleThreadedAsync(ruleSet.Rules, registries, fileSystem).ConfigureAwait(false) ?? Enumerable.Empty<TestResult>();
            }

            return results.Select(result =>
            {
                result.RuleSetName = ruleSet.Name;
                result.RuleSetPath = ruleSet.SourcePath;
                return result;
            });
        }

        private void SetPartContext()
        {
            // No-op: per-run Fabric workspace/item flow through the InspectionContext
            // pushed in ExecuteRuleSetsAsync. Method retained to keep call sites stable.
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

        private async Task<IFabricFileSystem> CreateFileSystemAsync()
        {
            if (!string.IsNullOrWhiteSpace(_args!.FabricWorkspaceId))
            {
                if (_tokenProvider == null)
                {
                    throw new InvalidOperationException("Authentication credential is required for Fabric workspace access.");
                }

                return string.IsNullOrWhiteSpace(_args!.FabricItem)
                    ? new FabricRemoteFileSystem(_args!.FabricWorkspaceId, _tokenProvider, _httpClient)
                    : await FabricRemoteFileSystem.CreateItemScopedAsync(_args!.FabricWorkspaceId, _args!.FabricItem, _tokenProvider, _httpClient).ConfigureAwait(false);
            }

            return new FabricLocalFileSystem(_args!.FabricItem ?? string.Empty);
        }

        private FabricRemoteFileSystem? SubscribeToProgressEvents(IFabricFileSystem fileSystem)
        {
            if (fileSystem is FabricRemoteFileSystem rfs)
            {
                rfs.ProgressReported += Insp_MessageIssued;
                return rfs;
            }
            return null;
        }

        private void UnsubscribeFromProgressEvents(FabricRemoteFileSystem? remoteFs)
        {
            if (remoteFs != null)
            {
                remoteFs.ProgressReported -= Insp_MessageIssued;
            }
        }

        private async Task<IEnumerable<TestResult>> RunInspectionAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem)
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

        private IEnumerable<TestResult> FilterResults(IEnumerable<TestResult> results)
        {
            return results.Where(_ => _args!.Verbose || !_.Pass);
        }

        private async Task<IEnumerable<TestResult>?> RunSingleThreadedAsync(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem)
        {
            return await RunSingleThreadedCoreAsync(rules, registries, fileSystem).ConfigureAwait(false);
        }

        private async Task OutputResultsAsync(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var orchestrator = new ResultOutputOrchestrator(
                _args!,
                _tokenProvider?.Credential,
                OnMessageIssued,
                OnMessageIssued,
                RaiseDialogMessage,
                CreateFileSystemAsync,
                DeserialiseRulesFromPath,
                RunInspectionAsync,
                SubscribeToProgressEvents,
                UnsubscribeFromProgressEvents);

            await orchestrator.ExecuteAsync(testResults, pageRenderer, registries).ConfigureAwait(false);
        }

        /// <summary>
        /// Deserialise rules from a local path or OneLake DFS URL. Uses this
        /// engine's token provider for OneLake authentication.
        /// </summary>
        public InspectionRules DeserialiseRulesFromPath(string rulesPath)
        {
            return RulesFileLoader.DeserialiseRulesFromPath(rulesPath, _tokenProvider,
                msg => OnMessageIssued(MessageTypeEnum.Information, msg));
        }

        private void Insp_MessageIssued(object? sender, MessageIssuedEventArgs e)
        {
            RaiseMessage(e);
        }

        private MessageIssuedEventArgs RaiseDialogMessage(MessageTypeEnum messageType, string message)
        {
            var args = new MessageIssuedEventArgs(message, messageType);
            MessageIssued?.Invoke(this, args);
            return args;
        }

        private void OnMessageIssued(MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(message, messageType);
            RaiseMessage(e);
        }

        private void OnMessageIssued(string itemPath, MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(itemPath, message, messageType);
            RaiseMessage(e);
        }

        private void RaiseMessage(MessageIssuedEventArgs e)
        {
            if (_args != null && (_args.ADOOutput || _args.GITHUBOutput))
            {
                if (e.MessageType == MessageTypeEnum.Error) IncrementErrorCount();
                if (e.MessageType == MessageTypeEnum.Warning) IncrementWarningCount();
            }

            MessageIssued?.Invoke(this, e);
        }
    }
}
