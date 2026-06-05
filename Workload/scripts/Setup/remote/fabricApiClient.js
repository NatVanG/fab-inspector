/**
 * Fabric API Client
 * Handles HTTP requests to Microsoft Fabric APIs
 */

const https = require('https');
const { URL } = require('url');

/**
 * Make HTTPS request to Fabric API
 * @param {string} url - Full URL to call
 * @param {string} authHeader - Authorization header value
 * @param {string} method - HTTP method (default: GET)
 * @param {object|null} body - Request body for POST/PUT/PATCH (default: null)
 * @returns {Promise<object>} Parsed JSON response
 */
function makeFabricApiRequest(url, authHeader, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const parsedUrl = new URL(url);
    const options = {
      hostname: parsedUrl.hostname,
      port: 443,
      path: parsedUrl.pathname + parsedUrl.search,
      method: method,
      headers: {
        'Authorization': authHeader,
        'Content-Type': 'application/json',
        'User-Agent': 'ms-fabric-extensibility-toolkit'
      }
    };

    const req = https.request(options, (res) => {
      let responseBody = '';
      res.on('data', (chunk) => { responseBody += chunk; });
      res.on('end', () => {
        try {
          const parsed = responseBody ? JSON.parse(responseBody) : {};
          if (res.statusCode >= 200 && res.statusCode < 300) {
            resolve(parsed);
          } else {
            const errorMsg = parsed.message || parsed.error?.message || `HTTP ${res.statusCode}`;
            const error = new Error(`Fabric API error: ${errorMsg}`);
            error.statusCode = res.statusCode;
            error.response = parsed;
            reject(error);
          }
        } catch (parseError) {
          reject(new Error(`Failed to parse Fabric API response: ${parseError.message}`));
        }
      });
    });

    req.on('error', (error) => {
      reject(error);
    });

    if (body) {
      req.write(JSON.stringify(body));
    }
    req.end();
  });
}

module.exports = {
  makeFabricApiRequest
};
