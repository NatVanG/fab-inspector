/**
 * Workload Item Lifecycle API implementation
 * Implements Microsoft Fabric Workload REST APIs for item lifecycle (CRUD operations)
 */

const express = require('express');
const { logRequest } = require('./utils');
const { authenticateControlPlaneCall } = require('./authentication');
const { appendToItemLog } = require('./itemLogger');

const router = express.Router();

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/onCreateItem
 * Create a new item
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/onCreateItem', async (req, res, next) => {
  const authResult = await authenticateControlPlaneCall(req, res);
  if (!authResult) return; // Auth failed, response already sent
  
  logRequest(req, 'CreateItem');
  
  const { workspaceId, itemId, itemType } = req.params;
  
  // Log CREATE operation to item's log file in OneLake
  try {
    await appendToItemLog(req, 'CREATE');
  } catch (error) {
    console.error(`❌[Backend] Failed to log item creation: ${error.message}`);
    return res.status(500).json({ 
      error: 'Failed to log item creation to OneLake', 
      message: error.message 
    });
  }
  
  console.log(`✅[Backend] ${req.method} ${req.path} - Created item: (${JSON.stringify(req.params)}) (${JSON.stringify(req.body)})`);
  res.status(200).json({});
});

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/onUpdateItem
 * Update an existing item
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/onUpdateItem', async (req, res, next) => {
  const authResult = await authenticateControlPlaneCall(req, res);
  if (!authResult) return; // Auth failed, response already sent
  
  logRequest(req, 'UpdateItem');
  
  const { workspaceId, itemId, itemType } = req.params;
  
  // Log UPDATE operation to item's log file in OneLake
  try {
    await appendToItemLog(req, 'UPDATE');
  } catch (error) {
    console.error(`❌[Backend] Failed to log item update: ${error.message}`);
    return res.status(500).json({ 
      error: 'Failed to log item update to OneLake', 
      message: error.message 
    });
  }
  
  console.log(`✅[Backend] ${req.method} ${req.path} - Updated item:(${JSON.stringify(req.params)}) (${JSON.stringify(req.body)})`);
  res.status(200).json({});
});

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/onDeleteItem
 * Delete an existing item
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/onDeleteItem', async (req, res, next) => {
  // Note: Subject token may not always be present during delete operations
  const authResult = await authenticateControlPlaneCall(req, res, { requireSubjectToken: false });
  if (!authResult) return; // Auth failed, response already sent
  const deleteType = req.body?.deleteType || 'Hard';
  const validDeleteTypes = ['Hard', 'Soft'];
  if (!validDeleteTypes.includes(deleteType)) {
    return res.status(400).json({
      error: 'InvalidParameter',
      message: `Invalid deleteType: ${deleteType}. Must be 'Hard' or 'Soft'`
    });
  }
  if (deleteType === 'Hard') {
    logRequest(req, 'HardDeleteItem');
  } else {
    try {
      await appendToItemLog(req, 'SOFTDELETE');
    } catch (error) {
      console.error(`❌[Backend] Failed to log item soft delete: ${error.message}`);
    }
    logRequest(req, 'SoftDeleteItem');
  }    
  console.log(`✅[Backend] ${req.method} ${req.path} - Deleted item: (${JSON.stringify(req.params)}) (${JSON.stringify(req.body)})`);
  res.status(200).json({ success: true });
});

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/onRestoreItem
 * Restore a deleted item
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/onRestoreItem', async (req, res, next) => {
  const authResult = await authenticateControlPlaneCall(req, res, { requireSubjectToken: false });
  if (!authResult) return; // Auth failed, response already sent
  logRequest(req, 'RestoreItem');
  try {
      await appendToItemLog(req, 'RESTORE');
    } catch (error) {
      console.error(`❌[Backend] Failed to log item restore: ${error.message}`);
      return res.status(500).json({
        error: 'Failed to log item restore to OneLake',
        message: error.message
      });
    }
  console.log(`✅[Backend] ${req.method} ${req.path} - Restored item: (${JSON.stringify(req.params)}) (${JSON.stringify(req.body)})`);
  res.status(200).json({ success: true });
});

module.exports = router;