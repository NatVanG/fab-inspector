using System.Threading.Channels;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;
using FabInspector.Core.Part;

namespace FabInspector.Web.Services;

/// <summary>
/// Form input collected by the inspection page.
/// </summary>
public sealed record InspectionRequest(
    string FabricWorkspaceId,
    string? FabricItemId,
    string RulesFileUrl,
    bool Verbose,
    bool Parallel,
    bool JsonOutput,
    bool HtmlOutput);

/// <summary>
/// Single completed inspection run plus log lines streamed during execution.
/// </summary>
public sealed record InspectionRunResult(
    TestRun? TestRun,
    IReadOnlyList<string> LogLines,
    Exception? Failure);

/// <summary>
/// Orchestrates one Fab Inspector run for the signed-in Blazor user.
///
/// IMPORTANT: <see cref="FabInspector.ClientLibrary.Main"/> retains process-wide
/// static state (<c>_args</c>, <c>_tokenProvider</c>, error/warning counters,
/// and the <c>WinMessageIssued</c> event). To keep concurrent users from stomping
/// on each other this service serialises all inspections through a
/// <see cref="SemaphoreSlim"/>. Removing the gate requires a deeper refactor of
/// <c>Main.cs</c>.
/// </summary>
public sealed class InspectionRunner
{
    // One in-flight inspection at a time, process-wide.
    private static readonly SemaphoreSlim _gate = new(1, 1);

    private readonly ITokenProvider _tokenProvider;
    private readonly IReportPageWireframeRenderer _pageRenderer;
    private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
    private readonly ILogger<InspectionRunner> _logger;

    public InspectionRunner(
        ITokenProvider tokenProvider,
        IReportPageWireframeRenderer pageRenderer,
        IEnumerable<JsonLogicOperatorRegistry> registries,
        ILogger<InspectionRunner> logger)
    {
        _tokenProvider = tokenProvider;
        _pageRenderer = pageRenderer;
        _registries = registries;
        _logger = logger;
    }

    /// <summary>
    /// Runs an inspection and streams progress lines to <paramref name="progress"/> as
    /// they are emitted. Awaits the gate first, so concurrent callers queue.
    /// </summary>
    public async Task<InspectionRunResult> RunAsync(
        InspectionRequest request,
        ChannelWriter<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();

        void Handler(object? sender, MessageIssuedEventArgs e)
        {
            var line = $"[{e.MessageType}] {(string.IsNullOrEmpty(e.ItemPath) ? "" : e.ItemPath + ": ")}{e.Message}";
            lines.Add(line);
            progress?.TryWrite(line);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var args = new Args
            {
                FabricWorkspaceId = request.FabricWorkspaceId,
                FabricItem = request.FabricItemId ?? string.Empty,
                RulesFilePath = request.RulesFileUrl,
                OutputPath = string.Empty,
                FormatsString = string.Concat(
                    request.JsonOutput ? "JSON" : string.Empty,
                    ",",
                    request.HtmlOutput ? "HTML" : string.Empty),
                VerboseString = request.Verbose.ToString(),
                ParallelString = request.Parallel.ToString(),
                AuthMethod = "external" // sentinel — RunAndReturnResultsAsync overload below ignores this
            };

            FabInspector.ClientLibrary.Main.WinMessageIssued += Handler;

            // Per-run ambient context for operators that still read ContextService.
            using var scope = ContextService.BeginScope(
                _tokenProvider,
                request.FabricWorkspaceId,
                request.FabricItemId);

            try
            {
                var testRun = await FabInspector.ClientLibrary.Main
                    .RunAndReturnResultsAsync(args, _tokenProvider, _pageRenderer, _registries)
                    .ConfigureAwait(false);

                progress?.TryComplete();
                return new InspectionRunResult(testRun, lines, Failure: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inspection run failed");
                lines.Add($"[Error] {ex.Message}");
                progress?.TryWrite($"[Error] {ex.Message}");
                progress?.TryComplete(ex);
                return new InspectionRunResult(TestRun: null, lines, Failure: ex);
            }
            finally
            {
                FabInspector.ClientLibrary.Main.WinMessageIssued -= Handler;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
