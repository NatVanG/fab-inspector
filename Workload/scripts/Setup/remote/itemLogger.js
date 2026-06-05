/**
 * Item Lifecycle Logger for OneLake
 * Logs item operations to OneLake storage with payload files
 */

const crypto = require('crypto');
const oneLakeClientService = require('./oneLakeClientService');
const tokenExchangeService = require('./tokenExchangeService');

const ENABLE_ONELAKE_LOGGING = process.env.ENABLE_ONELAKE_LOGGING === 'true';

if (ENABLE_ONELAKE_LOGGING) {
  console.log('✅[Backend] OneLake item logging is ENABLED');
} else {
  console.log('⚠️[Backend] OneLake item logging is DISABLED (set ENABLE_ONELAKE_LOGGING=true to enable)');
}

/**
 * Append a log entry to the item's log file in OneLake
 * Stores request payloads in separate files and only references them in the main log
 * @param {object} req - Express request object with authContext and params
 * @param {string} operation - Operation type (CREATE, UPDATE, SOFTDELETE, RESTORE)
 */
async function appendToItemLog(req, operation) {
  if (!ENABLE_ONELAKE_LOGGING) {
    return false;
  }
  
  const { workspaceId, itemId, itemType } = req.params;
  const { subjectToken, tenantId, userName } = req.authContext;
  const activityId = req.headers['x-ms-root-activity-id'] || crypto.randomUUID();
  const payload = req.body;
  
  // Exchange user token for OneLake-scoped token
  const oneLakeToken = await tokenExchangeService.getTokenForScope(subjectToken, tenantId, oneLakeClientService.ONELAKE_SCOPE);
  
  const logFilePath = oneLakeClientService.getOneLakeFilePath(workspaceId, itemId, 'item-log.txt');
  const timestamp = new Date().toISOString();
  
  // Store payload in separate file if provided
  let payloadFilePath = null;
  if (payload) {
    const payloadFileName = `request-${activityId}.json`;
    payloadFilePath = oneLakeClientService.getOneLakeFilePath(workspaceId, itemId, `requests/${payloadFileName}`);
    const payloadContent = JSON.stringify(payload, null, 2);
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, payloadFilePath, payloadContent);
    console.log(`✅[Backend] Created payload file: ${payloadFilePath}`);
  }
  
  // Create log entry with reference to payload file
  const metadataStr = itemType ? ` - ${JSON.stringify({ itemType })}` : '';
  const payloadRef = payloadFilePath ? ` - Payload: ${payloadFilePath}` : '';
  const logEntry = `[${timestamp}] ${operation} - User: ${userName || 'Unknown'}${metadataStr}${payloadRef}\n`;
  
  // Check if log file exists
  const fileExists = await oneLakeClientService.checkIfFileExists(oneLakeToken, logFilePath);
  
  if (!fileExists) {
    // Create new log file with header
    const header = `Item Lifecycle Log\nWorkspace ID: ${workspaceId}\nItem ID: ${itemId}\n${'='.repeat(80)}\n`;
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, logFilePath, header + logEntry);
    console.log(`✅[Backend] Created item log file: ${logFilePath}`);
  } else {
    // Read existing log and append new entry
    const existingLog = await oneLakeClientService.getOneLakeFile(oneLakeToken, logFilePath);
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, logFilePath, existingLog + logEntry);
    console.log(`✅[Backend] Appended to item log file: ${logFilePath}`);
  }
  
  return true;
 
}

module.exports = { appendToItemLog };
