/**
 * Jobs Lifecycle Logger for OneLake
 * Logs job operations to OneLake storage with payload files
 */

const crypto = require('crypto');
const oneLakeClientService = require('./oneLakeClientService');
const tokenExchangeService = require('./tokenExchangeService');

const ENABLE_ONELAKE_LOGGING = process.env.ENABLE_ONELAKE_LOGGING === 'true';

if (ENABLE_ONELAKE_LOGGING) {
  console.log('✅[Backend] OneLake jobs logging is ENABLED');
} else {
  console.log('⚠️[Backend] OneLake jobs logging is DISABLED (set ENABLE_ONELAKE_LOGGING=true to enable)');
}

/**
 * Append a log entry to the job's log file in OneLake
 * Stores request payloads in separate files and only references them in the main log
 * @param {object} req - Express request object with authContext and params
 * @param {string} operation - Operation type (CREATE_JOB, GET_JOB_STATUS, CANCEL_JOB)
 * @returns {Promise<boolean>} true if logging succeeded or is disabled, throws error if enabled and fails
 */
async function appendToJobLog(req, operation) {
  if (!ENABLE_ONELAKE_LOGGING) {
    return true; // Logging disabled, return success
  }
  
  const { workspaceId, itemId, itemType } = req.params;
  const { subjectToken, tenantId, userName } = req.authContext;
  const activityId = req.headers['x-ms-root-activity-id'] || crypto.randomUUID();
  const payload = req.body;
  
  // Exchange user token for OneLake-scoped token
  const oneLakeToken = await tokenExchangeService.getTokenForScope(subjectToken, tenantId, oneLakeClientService.ONELAKE_SCOPE);
  
  const logFilePath = oneLakeClientService.getOneLakeFilePath(workspaceId, itemId, 'jobs-log.txt');
  const timestamp = new Date().toISOString();
  
  // Store payload in separate file if provided
  let payloadFilePath = null;
  if (payload) {
    const payloadFileName = `job-request-${activityId}.json`;
    payloadFilePath = oneLakeClientService.getOneLakeFilePath(workspaceId, itemId, `jobs/requests/${payloadFileName}`);
    const payloadContent = JSON.stringify(payload, null, 2);
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, payloadFilePath, payloadContent);
    console.log(`✅[Backend] Created job payload file: ${payloadFilePath}`);
  }
  
  // Create log entry with reference to payload file
  const jobInfo = payload?.jobType ? ` - Job Type: ${payload.jobType}` : '';
  const jobInstanceId = payload?.jobInstanceId ? ` - Instance ID: ${payload.jobInstanceId}` : '';
  const metadataStr = itemType ? ` - Item Type: ${itemType}` : '';
  const payloadRef = payloadFilePath ? ` - Payload: ${payloadFilePath}` : '';
  const logEntry = `[${timestamp}] ${operation} - User: ${userName || 'Unknown'}${jobInfo}${jobInstanceId}${metadataStr}${payloadRef}\n`;
  
  // Check if log file exists
  const fileExists = await oneLakeClientService.checkIfFileExists(oneLakeToken, logFilePath);
  
  if (!fileExists) {
    // Create new log file with header
    const header = `Jobs Lifecycle Log\nWorkspace ID: ${workspaceId}\nItem ID: ${itemId}\n${'='.repeat(80)}\n`;
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, logFilePath, header + logEntry);
    console.log(`✅[Backend] Created jobs log file: ${logFilePath}`);
  } else {
    // Read existing log and append new entry
    const existingLog = await oneLakeClientService.getOneLakeFile(oneLakeToken, logFilePath);
    await oneLakeClientService.writeToOneLakeFile(oneLakeToken, logFilePath, existingLog + logEntry);
    console.log(`✅[Backend] Appended to jobs log file: ${logFilePath}`);
  }
  return true;
}

module.exports = { appendToJobLog };
