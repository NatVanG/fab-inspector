/**
 * Permissions Module for Microsoft Fabric Workload
 * Handles permission resolution and validation for Fabric items
 */

const { buildCompositeToken } = require('./tokenBuilder');
const { makeFabricApiRequest } = require('./fabricApiClient');

// Fabric workload control API base URL
const FABRIC_WORKLOAD_CONTROL_URL = 'https://api.fabric.microsoft.com';

// ==================== Permission Resolution ====================

/**
 * Call Fabric's resolvepermissions API to get user's permissions on an item
 * @param {string} compositeToken - SubjectAndAppToken formatted authorization header
 * @param {string} workspaceId - Workspace object ID
 * @param {string} itemId - Item object ID
 * @returns {Promise<object>} Response containing permissions array
 */
async function resolveItemPermissions(compositeToken, workspaceId, itemId) {
  const fabricApiUrl = process.env.FABRIC_WORKLOAD_CONTROL_URL || FABRIC_WORKLOAD_CONTROL_URL;
  const url = `${fabricApiUrl}/v1/workload-control/workspaces/${workspaceId}/items/${itemId}/resolvepermissions`;
  return await makeFabricApiRequest(url, compositeToken, 'GET');
}

/**
 * Get user's permissions on an item
 * Returns the raw permissions array from Fabric's resolvepermissions API
 * @param {object} authContext - Authentication context from authenticateControlPlaneCall
 * @param {string} workspaceId - Workspace object ID
 * @param {string} itemId - Item object ID
 * @returns {Promise<object>} Result with permissions array
 */
async function getItemPermissions(authContext, workspaceId, itemId) {
  // Build composite token for Fabric API call
  const compositeToken = await buildCompositeToken(authContext);

  // Resolve actual permissions from Fabric
  const permissionsResponse = await resolveItemPermissions(compositeToken, workspaceId, itemId);
  const permissions = permissionsResponse.permissions || [];

  return {
    permissions,
    workspaceId,
    itemId
  };
}

// ==================== Exports ====================

module.exports = {
  resolveItemPermissions,
  getItemPermissions
};
