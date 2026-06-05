/**
 * Authentication Module for Microsoft Fabric Workload
 * Handles Azure AD token validation and authentication for control plane calls
 * 
 * Required Environment Variables:
 * - TENANT_ID: Azure AD tenant ID of the workload publisher
 * - BACKEND_APPID: Azure AD application ID (used as audience for v2 tokens)
 * - BACKEND_AUDIENCE: Audience for v1 tokens (defaults to BACKEND_APPID if not set)
 * - FABRIC_CLIENT_FOR_WORKLOADS_APP_ID: Expected app ID for Fabric client tokens (optional)
 * - NODE_ENV: Environment mode (development/production) - affects validation strictness
 */

const jwt = require('jsonwebtoken');
const jwksClient = require('jwks-rsa');
const { createErrorResponse, isValidUUID } = require('./utils');

// ==================== JWKS Client Management ====================

// JWKS client for retrieving signing keys from Azure AD
const jwksClients = new Map();

// Azure AD production endpoint (configurable via environment variable)
const AAD_LOGIN_URL = process.env.AAD_LOGIN_URL || 'https://login.microsoftonline.com';

/**
 * Get or create a JWKS client for a given tenant
 * @param {string} tenantId - Azure AD tenant ID
 * @returns {object} JWKS client instance
 */
function getJwksClient(tenantId = 'common') {
  if (!jwksClients.has(tenantId)) {
    const client = jwksClient({
      jwksUri: `${AAD_LOGIN_URL}/${tenantId}/discovery/v2.0/keys`,
      cache: true,
      cacheMaxAge: 86400000, // 24 hours
      rateLimit: true,
      jwksRequestsPerMinute: 10
    });
    jwksClients.set(tenantId, client);
  }
  return jwksClients.get(tenantId);
}

/**
 * Get signing key from JWKS endpoint
 * @param {object} header - JWT token header
 * @param {string} tenantId - Azure AD tenant ID
 * @returns {Promise<string>} Public signing key
 */
function getSigningKey(header, tenantId) {
  return new Promise((resolve, reject) => {
    const client = getJwksClient(tenantId);
    client.getSigningKey(header.kid, (err, key) => {
      if (err) {
        reject(err);
      } else {
        const signingKey = key.getPublicKey();
        resolve(signingKey);
      }
    });
  });
}

// ==================== Token Validation ====================

/**
 * Validate JWT token against Azure AD
 * @param {string} token - JWT token to validate
 * @param {object} options - Validation options
 * @param {boolean} options.isAppOnly - Whether this is an app-only token
 * @param {string} options.tenantId - Expected tenant ID
 * @param {string} options.audience - Expected audience for v1 tokens
 * @param {string} options.clientId - Expected client ID for v2 tokens
 * @returns {Promise<object>} Validated token claims
 */
async function validateAadToken(token, options = {}) {
  const { isAppOnly = false, tenantId = null, audience = null, clientId = null } = options;

  try {
    // Decode token without verification to get header and payload
    const decoded = jwt.decode(token, { complete: true });
    if (!decoded) {
      console.error(`❌[Backend] Failed to decode JWT token (first 50 chars): ${token.substring(0, 50)}...`);
      throw new Error('Invalid token format - jwt.decode() returned null');
    }
    const { header, payload } = decoded;
    
    // Determine token version
    const tokenVersion = payload.ver;
    if (!['1.0', '2.0'].includes(tokenVersion)) {
      throw new Error(`Unsupported token version: ${tokenVersion}`);
    }

    // Get tenant from token
    const tokenTenantId = payload.tid;
    if (!tokenTenantId) {
      throw new Error('Token missing tenant ID claim');
    }

    // Get signing key
    const signingKey = await getSigningKey(header, tokenTenantId);

    // Verify token signature and claims
    const verifyOptions = {
      algorithms: ['RS256'],
      clockTolerance: 60 // 1 minute clock skew
    };

    // Verify audience
    if (audience || clientId) {
      const expectedAudience = tokenVersion === '1.0' ? audience : clientId;
      if (expectedAudience) {
        verifyOptions.audience = expectedAudience;
      }
    }

    // Verify the token
    const verified = jwt.verify(token, signingKey, verifyOptions);

    // Validate issuer format
    const expectedIssuer = tokenVersion === '1.0'
      ? `https://sts.windows.net/${tokenTenantId}/`
      : `${AAD_LOGIN_URL}/${tokenTenantId}/v2.0`;
    
    if (verified.iss !== expectedIssuer) {
      throw new Error(`Invalid issuer: expected ${expectedIssuer}, got ${verified.iss}`);
    }

    // Validate app-only vs delegated token
    const appIdClaim = tokenVersion === '1.0' ? 'appid' : 'azp';
    if (!verified[appIdClaim]) {
      throw new Error(`Token missing ${appIdClaim} claim`);
    }

    if (isAppOnly) {
      // App-only token validation
      if (verified.idtyp !== 'app') {
        throw new Error('Expected app-only token');
      }
      if (!verified.oid) {
        throw new Error('App-only token missing oid claim');
      }
      if (verified.scp) {
        throw new Error('App-only token should not have scp claim');
      }
    } else {
      // Delegated token validation
      if (verified.idtyp) {
        throw new Error('Delegated token should not have idtyp claim');
      }
      if (!verified.scp) {
        throw new Error('Delegated token missing scp claim');
      }
    }

    // Validate tenant if required
    if (tenantId && verified.tid !== tenantId) {
      throw new Error(`Token tenant mismatch: expected ${tenantId}, got ${verified.tid}`);
    }

    return verified;

  } catch (error) {
    console.error('❌[Backend] Token validation failed:', error.message);
    throw new Error(`Token validation failed: ${error.message}`);
  }
}

/**
 * Parse SubjectAndAppToken from Authorization header
 * @param {string} authHeader - Authorization header value
 * @returns {object|null} Parsed tokens or null if invalid format
 */
function parseSubjectAndAppToken(authHeader) {
  if (!authHeader || !authHeader.startsWith('SubjectAndAppToken1.0 ')) {
    return null;
  }

  const tokenPart = authHeader.substring('SubjectAndAppToken1.0 '.length);
  const tokens = {};
  
  const parts = tokenPart.split(',');

  for (const part of parts) {
    const eqIndex = part.indexOf('=');
    if (eqIndex === -1) continue;
    const key = part.substring(0, eqIndex).trim();
    const value = part.substring(eqIndex + 1).trim();
    if (key && value) {
      // Remove surrounding quotes from the token value
      tokens[key] = value.replace(/^"(.*)"$/, '$1');
    }
  }

  return {
    subjectToken: tokens.subjectToken || null,
    appToken: tokens.appToken || null
  };
}

// ==================== Authentication Middleware ====================

/**
 * Authentication middleware that validates control plane calls
 * Validates:
 * - Authorization header with SubjectAndAppToken scheme
 * - x-ms-client-tenant-id header
 * - Token signatures and claims against Azure AD
 * 
 * @param {object} req - Express request object
 * @param {object} res - Express response object
 * @param {function} next - Express next middleware function
 * @param {object} options - Authentication options
 * @param {boolean} options.requireSubjectToken - Whether subject token is required (default: true)
 * @param {boolean} options.requireTenantIdHeader - Whether tenant ID header is required (default: true)
 */
async function authenticateControlPlaneCall(req, res, options = {}) {
  const { requireSubjectToken = true, requireTenantIdHeader = true } = options;

  try {
    // Validate Authorization header
    const authHeader = req.headers['authorization'];

    if (!authHeader) {
      console.error('❌[Backend] Missing Authorization header');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Missing Authorization header',
        'System',
        true
      ));
    }

    const tokens = parseSubjectAndAppToken(authHeader);
    if (!tokens) {
      console.error('❌[Backend] Invalid Authorization header format');
      console.error(`   Header value: ${authHeader.substring(0, 50)}...`);
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Invalid Authorization header format. Expected SubjectAndAppToken1.0 scheme',
        'System',
        true
      ));
    }

    if (!tokens.appToken) {
      console.error('❌[Backend] Missing app token in Authorization header');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Missing app token in Authorization header',
        'System',
        true
      ));
    }

    if (requireSubjectToken && !tokens.subjectToken) {
      console.error('❌[Backend] Missing required subject token in Authorization header');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Missing subject token in Authorization header',
        'System',
        true
      ));
    }

    // Validate tenant ID header
    let tenantId = null;
    if (requireTenantIdHeader) {
      tenantId = req.headers['x-ms-client-tenant-id'];
      if (!tenantId || !isValidUUID(tenantId)) {
        console.error(`❌[Backend] Invalid or missing x-ms-client-tenant-id header: ${tenantId}`);
        return res.status(401).json(createErrorResponse(
          'Unauthorized',
          'Missing or invalid x-ms-client-tenant-id header',
          'System',
          true
        ));
      }
    }

    // Get configuration from environment
    const publisherTenantId = process.env.TENANT_ID;
    const audience = process.env.BACKEND_AUDIENCE;
    const clientId = process.env.BACKEND_APPID;
    const fabricBackendAppId = '00000009-0000-0000-c000-000000000000'; // Microsoft Fabric Backend service
    const fabricClientForWorkloadsAppId = process.env.FABRIC_CLIENT_FOR_WORKLOADS_APP_ID || 'd2450708-699c-41e3-8077-b0c8341509aa';

    // Validate app token (app-only token from Fabric)
    const appTokenClaims = await validateAadToken(tokens.appToken, {
      isAppOnly: true,
      audience,
      clientId
    });

    console.log('✅[Backend] App token validation successful');

    // Verify app token is from Fabric and in publisher's tenant
    const appTokenVersion = appTokenClaims.ver;
    const appIdClaim = appTokenVersion === '1.0' ? 'appid' : 'azp';
    const appTokenAppId = appTokenClaims[appIdClaim];
    
    // Validate app token belongs to Fabric
    const validFabricAppIds = [fabricBackendAppId, fabricClientForWorkloadsAppId];
    if (!validFabricAppIds.includes(appTokenAppId)) {
      console.warn(`⚠️[Backend] App token from unexpected app: ${appTokenAppId}`);
      console.warn(`   Expected one of: ${validFabricAppIds.join(', ')}`);
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'App token must belong to Fabric BE or Fabric client for workloads',
        'System',
        true
      ));
    }

    // Validate app token is in publisher's tenant
    if (publisherTenantId && appTokenClaims.tid !== publisherTenantId) {
      console.warn(`⚠️[Backend] App token tenant mismatch: expected ${publisherTenantId}, got ${appTokenClaims.tid}`);
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'App token must be in the publisher\'s tenant',
        'System',
        true
      ));
    }

    // Validate subject token if present
    let subjectTokenClaims = null;
    if (tokens.subjectToken) {
      subjectTokenClaims = await validateAadToken(tokens.subjectToken, {
        isAppOnly: false,
        tenantId: requireTenantIdHeader ? tenantId : null,
        audience,
        clientId
      });
      console.log('✅[Backend] Subject token validation successful');

      // Verify subject token belongs to same app as app token
      const subjectTokenVersion = subjectTokenClaims.ver;
      const subjectAppIdClaim = subjectTokenVersion === '1.0' ? 'appid' : 'azp';
      const subjectTokenAppId = subjectTokenClaims[subjectAppIdClaim];
      
      if (subjectTokenAppId !== appTokenAppId) {
        console.warn(`⚠️[Backend] Subject and app tokens from different apps: subject=${subjectTokenAppId}, app=${appTokenAppId}`);
      }

      // Verify subject token tenant matches header
      if (requireTenantIdHeader && subjectTokenClaims.tid !== tenantId) {
        console.error(`❌[Backend] Subject token tenant mismatch: header=${tenantId}, token=${subjectTokenClaims.tid}`);
        return res.status(401).json(createErrorResponse(
          'Unauthorized',
          'Subject token tenant must match x-ms-client-tenant-id header',
          'System',
          true
        ));
      }

      // Validate required scopes
      const scopes = (subjectTokenClaims.scp || '').split(' ');
      const requiredScope = 'FabricWorkloadControl'; // Fabric workload control scope
      if (!scopes.includes(requiredScope)) {
        console.warn(`⚠️[Backend] Missing required scope: ${requiredScope}. Found: ${scopes.join(', ')}`);
        return res.status(403).json(createErrorResponse(
          'Forbidden',
          'Missing required scopes',
          'System',
          true
        ));
      }
    }

    // Store authentication context in request
    req.authContext = {
      subjectToken: tokens.subjectToken,
      appToken: tokens.appToken,
      tenantId: tenantId,
      hasSubjectContext: !!tokens.subjectToken,
      appTokenClaims,
      subjectTokenClaims,
      userId: subjectTokenClaims?.oid || subjectTokenClaims?.sub,
      userName: subjectTokenClaims?.name || subjectTokenClaims?.upn
    };

    console.log(`✅[Backend] Authenticated control plane call - TenantId: ${tenantId || 'N/A'}, User: ${req.authContext.userName || 'N/A'}, HasSubject: ${req.authContext.hasSubjectContext}`);
    return true; // Authentication successful
  } catch (error) {
    console.error('❌[Backend] Authentication failed:', error.message);
    console.error('   Error stack:', error.stack);
    if (error.name === 'JsonWebTokenError') {
      console.error('   JWT Error details:', error);
    }
    res.status(401).json(createErrorResponse(
      'Unauthorized',
      error.message || 'Authentication failed',
      'System',
      true
    ));
    return false; // Authentication failed
  }
}

/**
 * Authentication middleware for data plane calls (Bearer token from frontend)
 * This is used when the frontend directly calls the backend with a user token.
 * 
 * Validates:
 * - Authorization header with Bearer scheme
 * - Token signature and claims against Azure AD
 * 
 * @param {object} req - Express request object
 * @param {object} res - Express response object
 * @returns {Promise<boolean>} True if authentication succeeded, false if it failed (response sent)
 */
async function authenticateDataPlaneCall(req, res) {
  try {
    const authHeader = req.headers['authorization'];

    if (!authHeader) {
      console.error('❌[Backend] Missing Authorization header');
      res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Missing Authorization header',
        'System',
        true
      ));
      return false;
    }

    // Check for Bearer token
    if (!authHeader.startsWith('Bearer ')) {
      console.error('❌[Backend] Invalid Authorization header format - expected Bearer token');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Invalid Authorization header format. Expected Bearer token',
        'System',
        true
      ));
    }

    const token = authHeader.substring('Bearer '.length);

    if (!token) {
      console.error('❌[Backend] Empty Bearer token');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Empty Bearer token',
        'System',
        true
      ));
    }

    // Decode token to get claims
    const decoded = jwt.decode(token, { complete: true });
    if (!decoded) {
      console.error('❌[Backend] Failed to decode Bearer token');
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Invalid token format',
        'System',
        true
      ));
    }

    const { payload } = decoded;
    const tenantId = payload.tid;

    // In production, validate the token signature
    if (process.env.NODE_ENV === 'production') {
      const audience = process.env.BACKEND_AUDIENCE;
      const clientId = process.env.BACKEND_APPID;
      
      await validateAadToken(token, {
        isAppOnly: false,
        audience,
        clientId
      });
    }

    // Store authentication context in request
    req.authContext = {
      subjectToken: token,  // The user's token that can be used for OBO
      appToken: null,
      tenantId: tenantId,
      hasSubjectContext: true,
      appTokenClaims: null,
      subjectTokenClaims: payload,
      userId: payload.oid || payload.sub,
      userName: payload.name || payload.upn || payload.preferred_username
    };

    console.log(`✅[Backend] Authenticated data plane call - TenantId: ${tenantId || 'N/A'}, User: ${req.authContext.userName || 'N/A'}`);
    return true;

  } catch (error) {
    console.error('❌[Backend] Data plane authentication failed:', error.message);
    res.status(401).json(createErrorResponse(
      'Unauthorized',
      error.message || 'Authentication failed',
      'System',
      true
    ));
    return false;
  }
}

// ==================== Exports ====================

module.exports = {
  authenticateControlPlaneCall,
  authenticateDataPlaneCall,
  validateAadToken,
  parseSubjectAndAppToken,
  getJwksClient,
  getSigningKey
};
