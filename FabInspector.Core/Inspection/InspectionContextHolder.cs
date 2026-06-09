using System;
using System.Collections.Generic;
using System.Threading;
using FabInspector.Core.Part;

namespace FabInspector.Core.Inspection
{
    /// <summary>
    /// Ambient accessor for the current <see cref="InspectionContext"/>. Backed by a
    /// single <see cref="AsyncLocal{T}"/> slot so concurrent inspection runs (Blazor
    /// Server, parallel CLI invocations, etc.) see their own state without leaking
    /// across async boundaries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This collapses the four <c>AsyncLocal</c> slots from the legacy
    /// <c>ContextService</c> down to one. Operators that cannot receive dependencies
    /// via constructor injection (because Json.Logic constructs them through a
    /// <see cref="System.Text.Json.Serialization.JsonConverter"/>) read state via
    /// <see cref="Current"/> at evaluation time.
    /// </para>
    /// <para>
    /// Hosts push a context for the duration of a single run via
    /// <see cref="PushScope(InspectionContext)"/>, which returns an
    /// <see cref="IDisposable"/> that restores the prior value on dispose.
    /// </para>
    /// </remarks>
    public static class InspectionContextHolder
    {
        private static readonly AsyncLocal<InspectionContext?> _slot = new();

        /// <summary>
        /// The ambient inspection context for the current async flow, or null if no
        /// scope has been pushed. Operators are expected to throw a clear
        /// <see cref="InvalidOperationException"/> when this is null on a code path
        /// that requires it.
        /// </summary>
        public static InspectionContext? Current => _slot.Value;

        /// <summary>
        /// Returns <see cref="Current"/> or throws a clear
        /// <see cref="InvalidOperationException"/> identifying which operator failed to
        /// find the context. Used by operators on code paths that cannot proceed without
        /// run-level state (HttpClient, TokenProvider).
        /// </summary>
        public static InspectionContext Require(string operatorName)
        {
            return _slot.Value
                ?? throw new InvalidOperationException(
                    $"InspectionContextHolder.Current is not configured. Operator '{operatorName}' requires a host (CLI/WinForm/Web) to push an InspectionContext via InspectionContextHolder.PushScope(...) before any inspection rule executes.");
        }

        /// <summary>
        /// Pushes <paramref name="context"/> onto the ambient slot for the current
        /// async flow. Dispose the returned scope to restore the prior value.
        /// Intended for the inspection engine and test helpers; hosts should call
        /// this once per inspection run.
        /// </summary>
        public static IDisposable PushScope(InspectionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var prior = _slot.Value;
            _slot.Value = context;
            return new Scope(prior);
        }

        /// <summary>
        /// Emits an operator progress message through the current context's
        /// <see cref="InspectionContext.MessageReporter"/>, if one is configured.
        /// No-op when there is no current scope, no reporter, or the message is
        /// blank.
        /// </summary>
        public static void ReportOperatorProgress(string operatorName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var current = _slot.Value;
            var reporter = current?.MessageReporter;
            if (reporter == null)
            {
                return;
            }

            var formattedMessage = FormatProgressMessage(current, operatorName, message);
            var args = string.IsNullOrWhiteSpace(current?.ItemPath)
                ? new MessageIssuedEventArgs(formattedMessage, MessageTypeEnum.Information)
                : new MessageIssuedEventArgs(current!.ItemPath!, formattedMessage, MessageTypeEnum.Information);

            reporter.Report(args);
        }

        private static string FormatProgressMessage(InspectionContext? current, string operatorName, string message)
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(current?.RuleName))
            {
                segments.Add($"Rule \"{current!.RuleName}\"");
            }

            if (!string.IsNullOrWhiteSpace(current?.Part?.FileSystemName))
            {
                segments.Add($"Part \"{current!.Part!.FileSystemName}\"");
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                segments.Add($"Operator \"{operatorName}\"");
            }

            segments.Add(message);
            return string.Join(" - ", segments);
        }

        private sealed class Scope : IDisposable
        {
            private readonly InspectionContext? _prior;
            private bool _disposed;

            public Scope(InspectionContext? prior)
            {
                _prior = prior;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _slot.Value = _prior;
            }
        }
    }
}
