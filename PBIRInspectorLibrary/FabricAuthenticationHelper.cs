using Microsoft.Identity.Client;
using System.Text.Json;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// Helper class for authenticating with Microsoft Fabric REST API
    /// </summary>
    public static class FabricAuthenticationHelper
    {
        /// <summary>
        /// The resource URI for Power BI / Fabric API
        /// </summary>
        public const string FabricResourceUri = "https://analysis.windows.net/powerbi/api";

        /// <summary>
        /// The default scope for Fabric API authentication
        /// </summary>
        //public const string FabricDefaultScope = "https://analysis.windows.net/powerbi/api/.default";
        public const string FabricDefaultScope = "https://api.fabric.microsoft.com/.default";

        /// <summary>
        /// Validates that an access token appears to be in the correct format
        /// </summary>
        /// <param name="accessToken">The access token to validate</param>
        /// <returns>True if the token appears valid, false otherwise</returns>
        public static bool IsTokenFormatValid(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            // Basic JWT format check (header.payload.signature)
            var parts = accessToken.Split('.');
            return parts.Length == 3;
        }

        /// <summary>
        /// Tests if an access token is valid by making a simple API call to list workspaces
        /// </summary>
        /// <param name="accessToken">The access token to test</param>
        /// <returns>True if the token is valid and can access the API, false otherwise</returns>
        public static async Task<bool> TestAccessTokenAsync(string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await httpClient.GetAsync("https://api.fabric.microsoft.com/v1/workspaces");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets workspace information to verify the workspace ID is correct
        /// </summary>
        /// <param name="workspaceId">The workspace ID to verify</param>
        /// <param name="accessToken">The access token</param>
        /// <returns>Workspace information if successful, null otherwise</returns>
        public static async Task<WorkspaceInfo?> GetWorkspaceInfoAsync(string workspaceId, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await httpClient.GetAsync($"https://api.fabric.microsoft.com/v1/workspaces/{workspaceId}");
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<WorkspaceInfo>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Workspace information
        /// </summary>
        public class WorkspaceInfo
        {
            public string? Id { get; set; }
            public string? DisplayName { get; set; }
            public string? Description { get; set; }
            public string? Type { get; set; }
            public string? CapacityId { get; set; }
        }

        public static async Task<string> AuthenticateWithDeviceCodeAsync(string clientId, string? tenantId = null, Action<string>? onDeviceCodeMessage = null)
        {
            try
            {
                var authority = $"https://login.microsoftonline.com/{tenantId ?? "organizations"}";
                /* var app = PublicClientApplicationBuilder.Create(clientId)
                    .WithAuthority(authority)
                    .WithDefaultRedirectUri()
                    .Build();
                */

                // Use AzureCloudInstance for better reliability
                //var app = PublicClientApplicationBuilder.Create(clientId)
                //    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId ?? "organizations")
                //    .WithRedirectUri("msalf4a0f4d6-194e-4501-8a8a-d67c666ff63e://auth")
                //    .Build();

                var app = PublicClientApplicationBuilder.Create(clientId)
                     .WithAuthority(authority)
                    .WithRedirectUri("http://localhost")
                    .Build();

                var scopes = new[] { FabricDefaultScope };

                var result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
                {
                    try
                    {
                        // Allow caller to handle the message instead of direct Console.WriteLine
                        if (onDeviceCodeMessage != null)
                        {
                            onDeviceCodeMessage(deviceCodeResult.Message);
                        }
                        else
                        {
                            Console.WriteLine(deviceCodeResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception if callback fails
                        Console.WriteLine($"Error displaying device code message: {ex.Message}");
                    }
                    return Task.CompletedTask;
                }).ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                throw new Exception($"MSAL Service error during device code authentication: {ex.Message}", ex);
            }
            catch (MsalClientException ex)
            {
                throw new Exception($"MSAL Client error during device code authentication: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error during device code authentication: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Authenticates using interactive browser flow (suitable for desktop apps)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="tenantId">Azure AD tenant ID (optional, uses "organizations" if null)</param>
        /// <returns>Access token for Fabric API</returns>
        public static async Task<string> AuthenticateInteractiveAsync(string clientId, string? tenantId = null)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId ?? "organizations"}";
            var app = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authority)
                .WithDefaultRedirectUri()
                .Build();

            var scopes = new[] { FabricDefaultScope };

            try
            {
                // Try silent authentication first
                var accounts = await app.GetAccountsAsync();
                var result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // Fall back to interactive authentication
                var result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                return result.AccessToken;
            }
        }

        /// <summary>
        /// Authenticates using client credentials flow (suitable for service-to-service authentication)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="clientSecret">Azure AD app client secret</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <returns>Access token for Fabric API</returns>
        public static async Task<string> AuthenticateWithClientSecretAsync(string clientId, string clientSecret, string tenantId)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}";
            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authority))
                .Build();

            var scopes = new[] { FabricDefaultScope };

            var result = await app.AcquireTokenForClient(scopes)
                .ExecuteAsync();

            return result.AccessToken;
        }

        /// <summary>
        /// Authenticates using certificate-based authentication (suitable for service-to-service authentication)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="certificate">X509 certificate for authentication</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <returns>Access token for Fabric API</returns>
        public static async Task<string> AuthenticateWithCertificateAsync(string clientId, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, string tenantId)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}";
            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithCertificate(certificate)
                .WithAuthority(new Uri(authority))
                .Build();

            var scopes = new[] { FabricDefaultScope };

            var result = await app.AcquireTokenForClient(scopes)
                .ExecuteAsync();

            return result.AccessToken;
        }
    }
}
