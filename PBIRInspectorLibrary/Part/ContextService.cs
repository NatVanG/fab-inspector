using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    public static class ContextService
    {
        private static readonly ThreadLocal<PartContext> _current = new();

        public static PartContext Current
        {
            get => _current.Value;
            internal set => _current.Value = value;
        }

        /// <summary>
        /// Authenticated HTTP client configured with a Bearer token for the Fabric REST API.
        /// Must be set after authentication is complete before any Fabric REST operator is invoked.
        /// </summary>
        public static HttpClient? HttpClient { get; set; }

        public static TokenCredential? Credential { get; set; }

        /// <summary>
        /// The Fabric workspace ID (GUID) used as the target for any REST API calls.
        /// </summary>
        public static string? FabricWorkspaceId { get; set; }

        /// <summary>
        /// The Fabric item file path (local) or ID (remote)
        /// </summary>
        public static string? FabricItem { get; set; }
    }
}
