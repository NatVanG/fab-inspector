using Azure.Core;

namespace FabInspector.Core
{
    public class AuthenticationHelper
    {
        /// <summary>
        /// Scopes for the Power BI / Fabric REST API (analysis.windows.net audience).
        /// Used by Power BI REST endpoints and legacy Fabric endpoints that share the same audience.
        /// </summary>
        public static readonly string[] FabricScopes = new[]
        {
            "https://analysis.windows.net/powerbi/api/.default"
        };

        public static readonly string[] PowerBIScopes = new[]
        {
            "https://analysis.windows.net/powerbi/api/.default"
        };

        /// <summary>
        /// Scopes for the Fabric Items REST API (api.fabric.microsoft.com audience).
        /// Used by the /v1/workspaces/{id}/items endpoints.
        /// </summary>
        public static readonly string[] FabricItemsApiScopes = new[]
        {
            "https://api.fabric.microsoft.com/.default"
        };

        public static readonly string[] OneLakeDfsScopes = new[]
        {
            "https://storage.azure.com/.default"
        };

        /// <summary>
        /// Creates an HttpRequestMessage with the correct Bearer token for the target API.
        /// Uses the <see cref="ITokenProvider"/> for cached, thread-safe token acquisition
        /// so that a single HttpClient can talk to multiple API audiences safely.
        /// </summary>
        public static async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
            HttpMethod method,
            string requestUri,
            ITokenProvider tokenProvider,
            string[] scopes,
            StringContent? stringContent = null,
            CancellationToken cancellationToken = default)
        {
            var token = await tokenProvider.GetTokenAsync(scopes, cancellationToken).ConfigureAwait(false);

            var request = new HttpRequestMessage(method, requestUri);
            request.Content = stringContent;
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return request;
        }
    }
}
