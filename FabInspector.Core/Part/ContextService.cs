using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FabInspector.Core.Part
{
    public static class ContextService
    {
        // AsyncLocal flows across awaits and is isolated per logical call chain. This is
        // critical for multi-user hosts (e.g. Blazor Server) where multiple inspections
        // run concurrently on shared thread-pool threads — ThreadLocal would leak state
        // between users after any await.
        private static readonly AsyncLocal<PartContext?> _current = new();
        private static readonly AsyncLocal<ITokenProvider?> _tokenProvider = new();
        private static readonly AsyncLocal<string?> _fabricWorkspaceId = new();
        private static readonly AsyncLocal<string?> _fabricItem = new();

        public static PartContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        /// <summary>
        /// HTTP client for sending API requests. Operators should use per-request Authorization
        /// headers via <see cref="TokenProvider"/> rather than relying on DefaultRequestHeaders.
        /// Kept as a process-wide singleton because it carries no per-user state.
        /// </summary>
        public static HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Centralised, caching token provider shared by all components within the current
        /// async flow. Backed by <see cref="AsyncLocal{T}"/> so concurrent inspection runs
        /// do not share or overwrite each other's tokens.
        /// </summary>
        public static ITokenProvider? TokenProvider
        {
            get => _tokenProvider.Value;
            set => _tokenProvider.Value = value;
        }

        /// <summary>
        /// The Fabric workspace ID (GUID) used as the target for any REST API calls.
        /// Backed by <see cref="AsyncLocal{T}"/> for per-run isolation.
        /// </summary>
        public static string? FabricWorkspaceId
        {
            get => _fabricWorkspaceId.Value;
            set => _fabricWorkspaceId.Value = value;
        }

        /// <summary>
        /// The Fabric item file path (local) or ID (remote).
        /// Backed by <see cref="AsyncLocal{T}"/> for per-run isolation.
        /// </summary>
        public static string? FabricItem
        {
            get => _fabricItem.Value;
            set => _fabricItem.Value = value;
        }

        /// <summary>
        /// Pushes a <see cref="PartContext"/> and any non-null per-run values it carries
        /// (<see cref="PartContext.TokenProvider"/>, <see cref="PartContext.FabricWorkspaceId"/>,
        /// <see cref="PartContext.FabricItem"/>) onto the ambient async-local slots. Dispose
        /// the returned scope to restore the previous values. Intended for callers (CLI,
        /// WinForm, Blazor InspectionRunner) that own the lifetime of a single inspection.
        /// </summary>
        public static IDisposable BeginScope(PartContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var prior = new ScopeState(
                _current.Value,
                _tokenProvider.Value,
                _fabricWorkspaceId.Value,
                _fabricItem.Value);

            _current.Value = context;
            if (context.TokenProvider != null) _tokenProvider.Value = context.TokenProvider;
            if (context.FabricWorkspaceId != null) _fabricWorkspaceId.Value = context.FabricWorkspaceId;
            if (context.FabricItem != null) _fabricItem.Value = context.FabricItem;

            return new Scope(prior);
        }

        /// <summary>
        /// Pushes per-run values onto the ambient async-local slots without requiring a
        /// fully-populated <see cref="PartContext"/>. Used by hosts that authenticate
        /// before any rule-specific context exists.
        /// </summary>
        public static IDisposable BeginScope(ITokenProvider? tokenProvider, string? fabricWorkspaceId, string? fabricItem)
        {
            var prior = new ScopeState(
                _current.Value,
                _tokenProvider.Value,
                _fabricWorkspaceId.Value,
                _fabricItem.Value);

            if (tokenProvider != null) _tokenProvider.Value = tokenProvider;
            if (fabricWorkspaceId != null) _fabricWorkspaceId.Value = fabricWorkspaceId;
            if (fabricItem != null) _fabricItem.Value = fabricItem;

            return new Scope(prior);
        }

        private readonly record struct ScopeState(
            PartContext? Current,
            ITokenProvider? TokenProvider,
            string? FabricWorkspaceId,
            string? FabricItem);

        private sealed class Scope : IDisposable
        {
            private readonly ScopeState _prior;
            private bool _disposed;

            public Scope(ScopeState prior)
            {
                _prior = prior;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _current.Value = _prior.Current;
                _tokenProvider.Value = _prior.TokenProvider;
                _fabricWorkspaceId.Value = _prior.FabricWorkspaceId;
                _fabricItem.Value = _prior.FabricItem;
            }
        }

        public static void ReportOperatorProgress(string operatorName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var current = _current.Value;
            var reporter = current?.MessageReporter;
            if (reporter == null)
            {
                return;
            }

            var formattedMessage = FormatProgressMessage(current, operatorName, message);
            var args = string.IsNullOrWhiteSpace(current?.ItemPath)
                ? new MessageIssuedEventArgs(formattedMessage, MessageTypeEnum.Information)
                : new MessageIssuedEventArgs(current.ItemPath!, formattedMessage, MessageTypeEnum.Information);

            reporter.Report(args);
        }

        private static string FormatProgressMessage(PartContext? current, string operatorName, string message)
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(current?.RuleName))
            {
                segments.Add($"Rule \"{current.RuleName}\"");
            }

            if (!string.IsNullOrWhiteSpace(current?.Part?.FileSystemName))
            {
                segments.Add($"Part \"{current.Part.FileSystemName}\"");
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                segments.Add($"Operator \"{operatorName}\"");
            }

            segments.Add(message);
            return string.Join(" - ", segments);
        }
    }
}

