/**
 * Token Builder Module for Microsoft Fabric Workload
 * Handles composite token construction (SubjectAndAppToken format)
 */

const tokenExchangeService = require('./tokenExchangeService');

/**
 * Generate SubjectAndAppToken authorization header value
 * Combines OBO token (user-delegated) and S2S token (app-only) for Fabric API calls
 * @param {string} subjectToken - OBO token representing the user
 * @param {string} appToken - S2S app-only token
 * @returns {string} SubjectAndAppToken formatted string
 */
function generateSubjectAndAppTokenHeader(subjectToken, appToken) {
  return `SubjectAndAppToken1.0 subjectToken="${subjectToken}", appToken="${appToken}"`;
}

/**
 * Build composite token for Fabric API calls
 * Exchanges user token for Fabric OBO token and acquires S2S token
 * @param {object} authContext - Authentication context from authenticateControlPlaneCall
 * @returns {Promise<string>} SubjectAndAppToken formatted authorization header value
 */
async function buildCompositeToken(authContext) {
  const { subjectToken, tenantId } = authContext;
  const publisherTenantId = process.env.TENANT_ID;

  if (!subjectToken) {
    throw new Error('Subject token is required to build composite token');
  }

  if (!tenantId) {
    throw new Error('Tenant ID is required to build composite token');
  }

  if (!publisherTenantId) {
    throw new Error('TENANT_ID environment variable is required for S2S token');
  }

  // Exchange user's subject token for Fabric OBO token
  const fabricOboToken = await tokenExchangeService.getFabricOboToken(subjectToken, tenantId);

  // Acquire S2S token using publisher tenant
  const fabricS2SToken = await tokenExchangeService.getFabricS2SToken(publisherTenantId);

  // Combine into SubjectAndAppToken format
  return generateSubjectAndAppTokenHeader(fabricOboToken, fabricS2SToken);
}

module.exports = {
  buildCompositeToken,
  generateSubjectAndAppTokenHeader
};
