using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;
using System.Net.Http;

namespace FabInspector.ClientLibrary
{
    /// <summary>
    /// Static facade preserved for CLI / WinForm / MCP back-compat. Each call
    /// constructs a fresh <see cref="InspectionEngine"/> (Phase 3 of the DI
    /// refactor) and forwards its <see cref="InspectionEngine.MessageIssued"/>
    /// events to the process-wide <see cref="WinMessageIssued"/> event. Error
    /// and warning counters are aggregated back into the static counters after
    /// each run completes.
    ///
    /// Hosts that need true concurrency (e.g. the Blazor InspectionRunner)
    /// should construct an <see cref="InspectionEngine"/> directly and
    /// subscribe to its instance event instead.
    /// </summary>
    public class Main
    {
        public static event EventHandler<MessageIssuedEventArgs>? WinMessageIssued;

        private static Args? _args = null;
        private static int _errorCount = 0;
        private static int _warningCount = 0;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static ITokenProvider? _tokenProvider = null;

        public static int ErrorCount => _errorCount;
        public static int WarningCount => _warningCount;

        public static void IncrementErrorCount() => Interlocked.Increment(ref _errorCount);
        public static void IncrementWarningCount() => Interlocked.Increment(ref _warningCount);

        public static async Task AttendedRun(string fabricWorkspaceId, string fabricItem, string rulesFilePath, string outputPath, bool verbose, bool parallel, bool jsonOutput, bool htmlOutput, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var formatsString = string.Concat(jsonOutput ? "JSON" : string.Empty, ",", htmlOutput ? "HTML" : string.Empty);
            var verboseString = verbose.ToString();
            var parallelString = parallel.ToString();

            string authmethod = "local";

            // Determine presence of auth-related args; if any are present, default to interactive auth.
            if (!string.IsNullOrWhiteSpace(fabricWorkspaceId) || Guid.TryParse(fabricItem, out _) || OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesFilePath) || OneLakeOutputUploader.IsOneLakeDfsUrl(outputPath))
            {
                authmethod = "interactive";
            }

            var args = new Args { FabricWorkspaceId = fabricWorkspaceId, FabricItem = fabricItem, RulesFilePath = rulesFilePath, OutputPath = outputPath, FormatsString = formatsString, VerboseString = verboseString, ParallelString = parallelString, AuthMethod = authmethod };

            await Run(args, pageRenderer, registries);
        }

        public static InspectionRules DeserialiseRulesFromPath(string rulesPath)
        {
            return RulesFileLoader.DeserialiseRulesFromPath(rulesPath, _tokenProvider,
                msg => WinMessageIssued?.Invoke(null, new MessageIssuedEventArgs(msg, MessageTypeEnum.Information)));
        }

        public static async Task Run(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            var engine = new InspectionEngine(_httpClient);
            using var hook = HookEngine(engine);
            try
            {
                await engine.RunAsync(args, pageRenderer, registries).ConfigureAwait(false);
            }
            finally
            {
                _tokenProvider = engine.TokenProvider ?? _tokenProvider;
            }
        }

        /// <summary>
        /// Runs inspection and returns structured results without writing files.
        /// Intended for programmatic consumers such as the MCP server mode.
        /// </summary>
        public static async Task<TestRun> RunAndReturnResultsAsync(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;

            var engine = new InspectionEngine(_httpClient);
            using var hook = HookEngine(engine);
            try
            {
                return await engine.RunAndReturnResultsAsync(args, pageRenderer, registries).ConfigureAwait(false);
            }
            finally
            {
                _tokenProvider = engine.TokenProvider ?? _tokenProvider;
            }
        }

        /// <summary>
        /// Overload that accepts a pre-built <see cref="ITokenProvider"/> (e.g. a delegated
        /// per-user token provider from a Blazor / ASP.NET host). Skips the
        /// <see cref="FabricAuthenticationHelper"/>-based credential construction entirely
        /// so the caller controls token lifetime and audience.
        /// Internally constructs a fresh <see cref="InspectionEngine"/> per call so
        /// concurrent hosts no longer share per-run state through static fields.
        /// </summary>
        public static async Task<TestRun> RunAndReturnResultsAsync(Args args, ITokenProvider tokenProvider, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            if (tokenProvider == null) throw new ArgumentNullException(nameof(tokenProvider));

            _args = args;
            _tokenProvider = tokenProvider;

            var engine = new InspectionEngine(_httpClient);
            using var hook = HookEngine(engine);
            return await engine.RunAndReturnResultsAsync(args, tokenProvider, pageRenderer, registries).ConfigureAwait(false);
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

        /// <summary>
        /// Subscribes a forwarder to the engine's per-run <see cref="InspectionEngine.MessageIssued"/>
        /// event that re-raises on the static <see cref="WinMessageIssued"/> event for
        /// back-compat with legacy CLI/WinForm subscribers. Returns an <see cref="IDisposable"/>
        /// that unsubscribes the forwarder AND aggregates the engine's per-run error/warning
        /// counters into the static facade counters once the run completes.
        /// </summary>
        private static IDisposable HookEngine(InspectionEngine engine)
        {
            EventHandler<MessageIssuedEventArgs> forwarder = (s, e) => WinMessageIssued?.Invoke(null, e);
            engine.MessageIssued += forwarder;
            return new EngineHook(engine, forwarder);
        }

        private sealed class EngineHook : IDisposable
        {
            private readonly InspectionEngine _engine;
            private readonly EventHandler<MessageIssuedEventArgs> _forwarder;
            private bool _disposed;

            public EngineHook(InspectionEngine engine, EventHandler<MessageIssuedEventArgs> forwarder)
            {
                _engine = engine;
                _forwarder = forwarder;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _engine.MessageIssued -= _forwarder;
                Interlocked.Add(ref _errorCount, _engine.ErrorCount);
                Interlocked.Add(ref _warningCount, _engine.WarningCount);
            }
        }
    }
}
