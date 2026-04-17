using Azure.Core;
using Azure.Identity;
using FabInspector.Core;

namespace FabInspector.ClientLibrary.Utils
{
    /// <summary>
    /// Helper class for authenticating with Microsoft Fabric REST API
    /// </summary>
    public static class FabricAuthenticationHelper
    { 


        /// <summary>
        /// Creates an interactive browser credential (suitable for desktop apps)
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="tenantId">Azure AD tenant ID (optional, uses "organizations" if null)</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateInteractiveCredential(string? clientId = null, string? tenantId = null)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new InteractiveBrowserCredential();
            }

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
        /// Creates a certificate credential for service principal authentication.
        /// Supports PEM, PFX, and P12 files. If <paramref name="certificatePassword"/> is provided,
        /// the certificate is loaded with that password; otherwise it is treated as unprotected.
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <param name="certificatePath">Path to the certificate file (.pem, .pfx, or .p12)</param>
        /// <param name="certificatePassword">Optional certificate password (for password-protected PFX/P12)</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateCertificateCredential(string clientId, string tenantId, string certificatePath, string? certificatePassword = null)
        {
            if (string.IsNullOrWhiteSpace(certificatePassword))
            {
                return new ClientCertificateCredential(tenantId, clientId, certificatePath);
            }

            var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                certificatePath, certificatePassword,
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);
            return new ClientCertificateCredential(tenantId, clientId, cert);
        }

        /// <summary>
        /// Creates a federated token credential for workload identity / service principal federation.
        /// </summary>
        /// <param name="clientId">Azure AD app client ID</param>
        /// <param name="federatedToken">The federated identity token (e.g. a GitHub Actions OIDC token)</param>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateFederatedTokenCredential(string clientId, string federatedToken, string tenantId)
        {
            return new ClientAssertionCredential(tenantId, clientId, _ => Task.FromResult(federatedToken));
        }

        /// <summary>
        /// Creates a managed identity credential.
        /// Pass <paramref name="clientId"/> for user-assigned managed identity, or leave null for system-assigned.
        /// </summary>
        /// <param name="clientId">Client ID for user-assigned managed identity, or null for system-assigned</param>
        /// <returns>TokenCredential for Fabric API</returns>
        public static TokenCredential CreateManagedIdentityCredential(string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            }

            return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
        }

    }
}
