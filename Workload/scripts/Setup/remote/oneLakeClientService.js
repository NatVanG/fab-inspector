/**
 * OneLake Client Service for Microsoft Fabric
 * Simple Node.js client for OneLake DFS API operations using user-delegated tokens
 */

const https = require('https');
const { URL } = require('url');

const ONELAKE_DFS_BASE_URL = process.env.ONELAKE_DFS_BASE_URL || 'https://onelake.dfs.fabric.microsoft.com';
const ONELAKE_SCOPE = 'https://storage.azure.com/.default';

console.log(`[OneLakeClientService] Using OneLake endpoint: ${ONELAKE_DFS_BASE_URL}`);

/**
 * Simple OneLake client for file operations
 */
class OneLakeClientService {
  /**
   * Check if a file exists in OneLake
   */
  async checkIfFileExists(token, filePath) {
    try {
      const url = `${ONELAKE_DFS_BASE_URL}/${filePath}?resource=file`;
      const response = await this._makeRequest('HEAD', url, token);
      return response.statusCode === 200;
    } catch (error) {
      console.error(`[OneLakeClientService] CheckIfFileExists failed: ${error.message}`);
      return false;
    }
  }

  /**
   * Create directory in OneLake (idempotent)
   */
  async createDirectoryIfNotExists(token, dirPath) {
    try {
      const url = `${ONELAKE_DFS_BASE_URL}/${dirPath}?resource=directory`;
      const response = await this._makeRequest('PUT', url, token, '');
      return response.statusCode === 201 || response.statusCode === 200 || response.statusCode === 409;
    } catch (error) {
      console.error(`[OneLakeClientService] CreateDirectory failed: ${error.message}`);
      return false;
    }
  }

  /**
   * Write content to a OneLake file (creates or overwrites)
   */
  async writeToOneLakeFile(token, filePath, content) {
    const dirPath = filePath.substring(0, filePath.lastIndexOf('/'));
    await this.createDirectoryIfNotExists(token, dirPath);
    
    const url = `${ONELAKE_DFS_BASE_URL}/${filePath}?resource=file`;
    const createResponse = await this._makeRequest('PUT', url, token, '');
    
    if (createResponse.statusCode < 200 || createResponse.statusCode >= 300) {
      throw new Error(`Failed to create file: ${createResponse.statusCode}`);
    }
    
    await this._appendContent(token, filePath, content);
  }

  /**
   * Read file content from OneLake
   */
  async getOneLakeFile(token, filePath) {
    try {
      const url = `${ONELAKE_DFS_BASE_URL}/${filePath}`;
      const response = await this._makeRequest('GET', url, token);
      
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw new Error(`Failed to read file: ${response.statusCode}`);
      }
      
      return response.body;
    } catch (error) {
      console.error(`[OneLakeClientService] GetOneLakeFile failed: ${error.message}`);
      return '';
    }
  }

  /**
   * Helper to construct OneLake file path
   */
  getOneLakeFilePath(workspaceId, itemId, fileName) {
    return `${workspaceId}/${itemId}/Files/${fileName}`;
  }

  /**
   * Append content to file using OneLake DFS API (append + flush pattern)
   */
  async _appendContent(token, filePath, content) {
    const baseUrl = `${ONELAKE_DFS_BASE_URL}/${filePath}`;
    const contentLength = Buffer.byteLength(content, 'utf8');
    
    // Step 1: Append content
    const appendUrl = `${baseUrl}?position=0&action=append`;
    const appendResponse = await this._makeRequest('PATCH', appendUrl, token, content, 'application/json');
    
    if (appendResponse.statusCode < 200 || appendResponse.statusCode >= 300) {
      throw new Error(`Append failed: ${appendResponse.statusCode}`);
    }
    
    // Step 2: Flush to finalize
    const flushUrl = `${baseUrl}?position=${contentLength}&action=flush`;
    const flushResponse = await this._makeRequest('PATCH', flushUrl, token, null);
    
    if (flushResponse.statusCode < 200 || flushResponse.statusCode >= 300) {
      throw new Error(`Flush failed: ${flushResponse.statusCode}`);
    }
  }

  /**
   * Internal HTTPS request helper for OneLake DFS API
   */
  _makeRequest(method, url, token, body = null, contentType = 'application/json') {
    return new Promise((resolve, reject) => {
      const parsedUrl = new URL(url);
      const options = {
        hostname: parsedUrl.hostname,
        port: parsedUrl.port || 443,
        path: parsedUrl.pathname + parsedUrl.search,
        method: method,
        headers: {
          'Authorization': `Bearer ${token}`,
          'x-ms-version': '2021-06-08'
        }
      };
      
      if (body !== null && method !== 'HEAD') {
        const bodyBuffer = Buffer.from(body, 'utf8');
        options.headers['Content-Type'] = contentType;
        options.headers['Content-Length'] = bodyBuffer.length;
      }
      
      const req = https.request(options, (res) => {
        let responseBody = '';
        res.on('data', (chunk) => { responseBody += chunk; });
        res.on('end', () => {
          resolve({ statusCode: res.statusCode, headers: res.headers, body: responseBody });
        });
      });
      
      req.on('error', reject);
      if (body !== null && method !== 'HEAD') req.write(body);
      req.end();
    });
  }
}

// Export singleton instance with ONELAKE_SCOPE constant
const instance = new OneLakeClientService();
instance.ONELAKE_SCOPE = ONELAKE_SCOPE;
module.exports = instance;
