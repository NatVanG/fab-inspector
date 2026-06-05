/**
 * Token Exchange Service for Microsoft Fabric
 * Implements On-Behalf-Of (OBO) flow to exchange user tokens for resource-specific tokens
 */

const https = require('https');
const { URL } = require('url');

const AAD_LOGIN_URL = 'https://login.microsoftonline.com';
const ONELAKE_SCOPE = 'https://storage.azure.com/.default';
const DEFAULT_FABRIC_BACKEND_RESOURCEID = 'https://analysis.windows.net/powerbi/api';

/**
 * Token Exchange Service - OAuth 2.0 On-Behalf-Of flow
 */
class TokenExchangeService {
  /**
   * Get token for any scope using OBO flow
   * @param {string} userToken - User's access token
   * @param {string} tenantId - User's tenant ID
   * @param {string} scope - Target resource scope (e.g., 'https://storage.azure.com/.default')
   * @returns {Promise<string>} Access token for the requested scope
   */
  async getTokenForScope(userToken, tenantId, scope) {
    const clientId = process.env.BACKEND_APPID;
    const clientSecret = process.env.BACKEND_CLIENT_SECRET;

    if (!clientId || !clientSecret) {
      throw new Error('BACKEND_APPID and BACKEND_CLIENT_SECRET required for token exchange');
    }

    if (!tenantId) {
      throw new Error('Tenant ID required for token exchange');
    }
    
    if (!scope) {
      throw new Error('Scope required for token exchange');
    }
    
    const tokenEndpoint = `${AAD_LOGIN_URL}/${tenantId}/oauth2/v2.0/token`;
    const requestBody = new URLSearchParams({
      grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer',
      client_id: clientId,
      client_secret: clientSecret,
      assertion: userToken,
      scope: scope,
      requested_token_use: 'on_behalf_of'
    }).toString();

    const response = await this._makeTokenRequest(tokenEndpoint, requestBody);

    if (!response.access_token) {
      throw new Error('Token exchange response missing access_token');
    }

    return response.access_token;
  }

  /**
   * Get OneLake token (convenience wrapper)
   * @param {string} userToken - User's access token
   * @param {string} tenantId - User's tenant ID
   * @returns {Promise<string>} OneLake access token
   */
  async getOneLakeToken(userToken, tenantId) {
    return this.getTokenForScope(userToken, tenantId, ONELAKE_SCOPE);
  }

  /**
   * Get Fabric OBO token (convenience wrapper)
   * @param {string} userToken - User's access token
   * @param {string} tenantId - User's tenant ID
   * @returns {Promise<string>} Fabric OBO access token
   */
  async getFabricOboToken(userToken, tenantId) {
    const fabricResourceId = process.env.FABRIC_BACKEND_RESOURCEID || DEFAULT_FABRIC_BACKEND_RESOURCEID;
    return this.getTokenForScope(userToken, tenantId, `${fabricResourceId}/.default`);
  }

  /**
   * Get S2S (Service-to-Service) token using client credentials flow
   * This is an app-only token for the workload's backend to call APIs
   * @param {string} tenantId - Tenant ID (typically publisher's tenant where the app registration lives)
   * @param {string} scope - Target resource scope (e.g., 'https://analysis.windows.net/powerbi/api/.default')
   * @returns {Promise<string>} S2S access token for the requested scope
   */
  async getS2STokenForScope(tenantId, scope) {
    const clientId = process.env.BACKEND_APPID;
    const clientSecret = process.env.BACKEND_CLIENT_SECRET;

    if (!clientId || !clientSecret) {
      throw new Error('BACKEND_APPID and BACKEND_CLIENT_SECRET required for S2S token');
    }

    if (!tenantId) {
      throw new Error('Tenant ID required for S2S token');
    }

    const tokenEndpoint = `${AAD_LOGIN_URL}/${tenantId}/oauth2/v2.0/token`;
    const requestBody = new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
      scope: scope
    }).toString();

    const response = await this._makeTokenRequest(tokenEndpoint, requestBody);

    if (!response.access_token) {
      throw new Error('S2S token acquisition response missing access_token');
    }

    return response.access_token;
  }

  /**
   * Get Fabric S2S token (convenience wrapper)
   * @param {string} publisherTenantId - Publisher's tenant ID (where the app registration lives)
   * @returns {Promise<string>} Fabric S2S access token
   */
  async getFabricS2SToken(publisherTenantId) {
    const fabricResourceId = process.env.FABRIC_BACKEND_RESOURCEID || DEFAULT_FABRIC_BACKEND_RESOURCEID;
    return this.getS2STokenForScope(publisherTenantId, `${fabricResourceId}/.default`);
  }

  /**
   * Make HTTPS request to token endpoint
   */
  _makeTokenRequest(url, body) {
    return new Promise((resolve, reject) => {
      const parsedUrl = new URL(url);
      const options = {
        hostname: parsedUrl.hostname,
        port: 443,
        path: parsedUrl.pathname,
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          'Content-Length': Buffer.byteLength(body)
        }
      };
      
      const req = https.request(options, (res) => {
        let responseBody = '';
        res.on('data', (chunk) => { responseBody += chunk; });
        res.on('end', () => {
          try {
            const parsed = JSON.parse(responseBody);
            if (res.statusCode >= 200 && res.statusCode < 300) {
              resolve(parsed);
            } else {
              const errorMsg = parsed.error_description || parsed.error || `HTTP ${res.statusCode}`;
              reject(new Error(`Token exchange failed: ${errorMsg}`));
            }
          } catch (error) {
            reject(new Error(`Failed to parse token response: ${error.message}`));
          }
        });
      });
      
      req.on('error', reject);
      req.write(body);
      req.end();
    });
  }
}

// Export singleton instance
module.exports = new TokenExchangeService();
