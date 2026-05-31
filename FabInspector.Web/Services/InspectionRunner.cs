using System.Threading.Channels;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Output;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;

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
/// Constructs a fresh <see cref="InspectionEngine"/> per call and subscribes to
/// its per-instance <see cref="InspectionEngine.MessageIssued"/> event, so
/// concurrent users no longer share state through the process-wide
/// <c>Main.WinMessageIssued</c> event. The legacy serialising semaphore was
/// removed in Phase 4 of the DI refactor.
/// </summary>
public sealed class InspectionRunner
{
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
    /// they are emitted. Safe to call concurrently — each call constructs its own
    /// <see cref="InspectionEngine"/> instance with isolated per-run state.
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

        var engine = new InspectionEngine();
        engine.MessageIssued += Handler;

        try
        {
            var testRun = await engine
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
            engine.MessageIssued -= Handler;
        }
    }
}
