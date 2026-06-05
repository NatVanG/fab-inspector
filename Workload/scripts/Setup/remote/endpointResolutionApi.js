/**
 * Workload Endpoint Resolution API implementation
 * Implements Microsoft Fabric Workload Endpoint Resolution REST API
 */

const express = require('express');
const { logRequest } = require('./utils');
const { authenticateControlPlaneCall } = require('./authentication');

const router = express.Router();

/**
 * POST /resolve-api-path-placeholder
 * Resolve endpoint based on context
 * 
 * Uses relaxed authentication: app token required, but subject token and tenant ID header optional
*/
router.post('/resolve-endpoint', async (req, res, next) => {
  // Apply relaxed authentication
  const authResult = await authenticateControlPlaneCall(req, res, {
    requireSubjectToken: false,
    requireTenantIdHeader: false
  });
  if (!authResult) return; // Auth failed, response already sent

  logRequest(req, 'ResolveEndpoint');
  
  // Endpoint resolution - uses FRONTEND_URL from environment
  // In production, implement custom logic based on tenant/workspace context
  let resolvedUrl = process.env.FRONTEND_URL || 'http://localhost:60006/';
  
  const response = {
    url: resolvedUrl,
    ttlInMinutes: 60 // Cache for 1 hour
  };
  
  console.log(`✅[Backend] Resolved endpoint: ${resolvedUrl} (TTL: 60 min)`);
  res.status(200).json(response);
});

module.exports = router;
