using Azure.Core;
using Azure.Identity;
using PBIRInspectorLibrary;

namespace PBIRInspectorClientLibrary.Utils
{
    /// <summary>
    /// Helper class for authenticating with Microsoft Fabric REST API
    /// </summary>
    public static class FabricAuthenticationHelper
    { 


        /// <summary>
        /// Creates a device code credential with Fabric API scopes
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="tenantId">Azure AD tenant ID (optional, uses "organizations" if null)</param>
        /// <param name="onDeviceCodeMessage">Optional callback for device code messages</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateDeviceCodeCredential(string clientId, string? tenantId = null, Action<string>? onDeviceCodeMessage = null)
        {
            var options = new DeviceCodeCredentialOptions
            {
                ClientId = clientId,
                TenantId = tenantId ?? "organizations",
                // AddauthorityHost if needed for specific clouds
                // AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                DeviceCodeCallback = (deviceCodeInfo, cancellationToken) =>
                {
                    try
                    {
                        // Allow caller to handle the message instead of direct Console.WriteLine
                        if (onDeviceCodeMessage != null)
                        {
                            onDeviceCodeMessage(deviceCodeInfo.Message);
                        }
                        else
                        {
                            Console.WriteLine(deviceCodeInfo.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception if callback fails
                        Console.WriteLine($"Error displaying device code message: {ex.Message}");
                    }
                    return Task.CompletedTask;
                }
            };

            return new DeviceCodeCredential(options);
        }

        /// <summary>
        /// Creates an interactive browser credential (suitable for desktop apps)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="tenantId">Azure AD tenant ID (optional, uses "organizations" if null)</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateInteractiveCredential(string clientId, string? tenantId = null)
        {
            var options = new InteractiveBrowserCredentialOptions
            {
                ClientId = clientId,
                TenantId = tenantId ?? "organizations",
                RedirectUri = new Uri("http://localhost")
            };

            return new InteractiveBrowserCredential(options);
        }

        /// <summary>
        /// Creates a client secret credential (suitable for service-to-service authentication)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="clientSecret">Azure AD app client secret</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateClientSecretCredential(string clientId, string clientSecret, string tenantId)
        {
            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        /// <summary>
        /// Creates a certificate credential (suitable for service-to-service authentication)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="certificate">X509 certificate for authentication</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateCertificateCredential(string clientId, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, string tenantId)
        {
            return new ClientCertificateCredential(tenantId, clientId, certificate);
        }

        /// <summary>
        /// Gets a Fabric API access token for testing/debugging purposes
        /// </summary>
        /// <param name="credential">TokenCredential to use</param>
        /// <returns>Access token string</returns>
        //public static async Task<string> GetAccessTokenAsync(TokenCredential credential)
        //{
        //    var tokenRequestContext = new TokenRequestContext(AuthenticationHelper.FabricScopes);
        //    var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
        //    return token.Token;
        //}

    }
}
