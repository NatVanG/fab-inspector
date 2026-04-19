using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FabInspector.Core.Part
{
    public static class ContextService
    {
        private static readonly ThreadLocal<PartContext?> _current = new();

        public static PartContext? Current
        {
            get => _current.Value;
            internal set => _current.Value = value;
        }

        /// <summary>
        /// HTTP client for sending API requests. Operators should use per-request Authorization
        /// headers via <see cref="TokenProvider"/> rather than relying on DefaultRequestHeaders.
        /// </summary>
        public static HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Centralised, caching token provider shared by all components.
        /// Use <see cref="ITokenProvider.GetTokenAsync"/> for bearer tokens and
        /// <see cref="ITokenProvider.Credential"/> when an SDK needs a raw <see cref="TokenCredential"/>.
        /// </summary>
        public static ITokenProvider? TokenProvider { get; set; }

        /// <summary>
        /// The Fabric workspace ID (GUID) used as the target for any REST API calls.
        /// </summary>
        public static string? FabricWorkspaceId { get; set; }

        /// <summary>
        /// The Fabric item file path (local) or ID (remote)
        /// </summary>
        public static string? FabricItem { get; set; }

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
