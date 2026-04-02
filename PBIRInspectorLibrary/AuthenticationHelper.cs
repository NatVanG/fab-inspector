using Azure.Core;

namespace PBIRInspectorLibrary
{
    public class AuthenticationHelper
    {
        /// <summary>
        /// Required scopes for Microsoft Fabric REST API
        /// </summary>
        /// additional scopes? "https://onelake.dfs.fabric.microsoft.com/.default", "storage.azure.com/.default"
        public static readonly string[] FabricScopes = new[]
        {
            "https://analysis.windows.net/powerbi/api/.default"
        };

        public static readonly string[] PowerBIScopes = new[]
        {
            "https://analysis.windows.net/powerbi/api/.default"
        };

        /// <summary>
        /// Creates an HttpRequestMessage with the correct Bearer token for the target API.
        /// Use this instead of setting DefaultRequestHeaders so a single HttpClient
        /// can talk to both the Fabric API and the Power BI API simultaneously.
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="requestUri">Request URI</param>
        /// <param name="credential">TokenCredential to use</param>
        /// <param name="scopes">Scopes to request (use FabricScopes or PowerBIScopes)</param>
        /// <returns>HttpRequestMessage with Authorization header set</returns>
        public static async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
            HttpMethod method,
            string requestUri,
            TokenCredential credential,
            string[] scopes,
            StringContent? stringContent = null,
            CancellationToken cancellationToken = default)
        {
            var tokenRequestContext = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);

            var request = new HttpRequestMessage(method, requestUri);
            request.Content = stringContent;
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            return request;
        }
    }
}
