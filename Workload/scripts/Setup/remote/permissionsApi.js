/**
 * Permissions API for Microsoft Fabric Workload
 * Provides endpoint for getting user permission level on Fabric items
 * 
 * This module implements permission checking by:
 * 1. Authenticating the incoming data plane call
 * 2. Building a composite token (OBO + S2S) for Fabric API calls
 * 3. Calling Fabric's resolvepermissions API
 * 4. Returning the user's permission level (None, Read, ReadWrite, Owner)
 */

const express = require('express');
const { authenticateDataPlaneCall } = require('./authentication');
const { getItemPermissions } = require('./permissions');
const { createErrorResponse } = require('./utils');

const router = express.Router();

/**
 * GET /workspaces/:workspaceId/items/:itemId/permissions
 * 
 * Gets the authenticated user's permissions on an item.
 * Returns the raw permissions array from Fabric.
 * 
 * Success response (200):
 * {
 *   "permissions": ["Read", "Write", ...],  // Raw permissions from Fabric
 *   "workspaceId": "...",
 *   "itemId": "..."
 * }
 */
router.get('/workspaces/:workspaceId/items/:itemId/permissions', async (req, res) => {
  const { workspaceId, itemId } = req.params;

  try {
    // Authenticate the data plane call
    let authResult = await authenticateDataPlaneCall(req, res);
    if (!authResult) {
      return;
    }

    // Get permissions using Fabric API
    const result = await getItemPermissions(
      req.authContext,
      workspaceId,
      itemId
    );

    console.log('[Permissions] User permissions:', result.permissions.join(', ') || 'None');
    return res.status(200).json(result);

  } catch (error) {
    console.error('[Permissions] Error:', error.message);

    // Handle specific error types
    if (error.statusCode === 401) {
      return res.status(401).json(createErrorResponse(
        'Unauthorized',
        'Failed to authenticate with Fabric API: ' + error.message,
        'Permissions',
        true
      ));
    }

    if (error.statusCode === 403) {
      return res.status(403).json(createErrorResponse(
        'Forbidden',
        'Access denied by Fabric API: ' + error.message,
        'Permissions',
        false
      ));
    }

    if (error.statusCode === 404) {
      return res.status(404).json(createErrorResponse(
        'NotFound',
        'Item or workspace not found: ' + error.message,
        'Permissions',
        false
      ));
    }

    // Generic server error
    return res.status(500).json(createErrorResponse(
      'InternalError',
      'Failed to get permissions: ' + error.message,
      'Permissions',
      true
    ));
  }
});

module.exports = router;
