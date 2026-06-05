/**
 * Workload Jobs API implementation
 * Implements Microsoft Fabric Workload REST APIs for job scheduling operations
 */

const express = require('express');
const { logRequest } = require('./utils');
const { authenticateControlPlaneCall } = require('./authentication');
const { appendToJobLog } = require('./jobsLogger');

const router = express.Router();

// In-memory job state storage
// Key: jobInstanceId, Value: { status, startTimeUtc, endTimeUtc, errorDetails }
const jobStates = new Map();


/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onCreateJobInstance
 * Create and start a new job instance
 *
 * Request body: CreateItemJobInstanceRequest {
 *   jobType: string,
 *   jobInstanceId: string (uuid),
 *   executionData: object (optional),
 *   definition: ItemDefinition (optional)
 * }
 *
 * Response: 201 Created with ItemJobInstanceState
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/jobs/onCreateJobInstance', async (req, res, next) => {
  logRequest(req, 'CreateItemJobInstance');

  if (!await checkAuthentication(req, res)) {
    return; // Auth failed, response already sent
  }

  const startTimeUtc = new Date().toISOString();
  const { jobInstanceId } = req.body;
  
  // Initialize job state immediately
  const initialState = {
    status: 'InProgress',
    startTimeUtc: startTimeUtc
  };
  
  if (jobInstanceId) {
    jobStates.set(jobInstanceId, initialState);
  }
  
  //jobs need to be handled asynchronously to simulate real processing
  handleCreateJob(req, jobInstanceId);

  res.status(201).json(getJobInstanceState(jobInstanceId));
});

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onGetJobInstanceStatus
 * Get job instance state
 *
 * Request body: JobOperationRequest {
 *   jobType: string,
 *   jobInstanceId: string (uuid)
 * }
 *
 * Response: 200 OK with ItemJobInstanceState {
 *   status: JobInstanceStatus,
 *   startTimeUtc: string (date-time),
 *   endTimeUtc: string (date-time),
 *   errorDetails: ErrorDetails (optional)
 * }
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/jobs/onGetJobInstanceStatus', async (req, res, next) => {
  logRequest(req, 'GetItemJobInstanceState');

  if (!await checkAuthentication(req, res)) {
    return; // Auth failed, response already sent
  }

  const { jobInstanceId } = req.body;

  return res.status(200).json(getJobInstanceState(jobInstanceId));
});

/**
 * POST /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onCancelJobInstance
 * Cancel a job instance
 *
 * Request body: JobOperationRequest {
 *   jobType: string,
 *   jobInstanceId: string (uuid)
 * }
 *
 * Response: 200 OK with ItemJobInstanceState
 */
router.post('/workspaces/:workspaceId/items/:itemType/:itemId/jobs/onCancelJobInstance', async (req, res, next) => {
  logRequest(req, 'CancelItemJobInstance');

  if (!await checkAuthentication(req, res)) {
    return; // Auth failed, response already sent
  }

  const { jobInstanceId } = req.body;

  const jobInstance = getJobInstanceState(jobInstanceId);
  if(jobInstance && !jobInstance.endTimeUtc){
    const errorState =  {
      ...jobInstance,
      status: 'Cancelled',
      endTimeUtc: new Date().toISOString()
    }
    jobStates.set(jobInstanceId, errorState);
  } else {
    console.warn(`⚠️[Backend] Attempted to cancel non-existent or already completed job instance: ${jobInstanceId}`);
  }
  
  res.status(200).json(jobInstance);
});

/**
 * Get the current state of a job instance
 * @param {*} jobInstanceId 
 * @returns The job instance state object for this jobInstanceId
 */
function getJobInstanceState(jobInstanceId){
   if (jobInstanceId && jobStates.has(jobInstanceId)) {
    return jobStates.get(jobInstanceId);
   } else {
    return {
      status: 'Failed',
      endTimeUtc: new Date().toISOString(),
      errorDetails: {
        errorCode: 'InvalidRequest',
        message: 'jobInstanceId is required'
      }
    };
  }
}

/** * Helper to check authentication and return early if failed
 * @param {object} req - Express request object
 * @param {object} res - Express response object
 * @returns {Promise<boolean>} true if authenticated, false if response already sent
 */
async function checkAuthentication(req, res) {
  const authResult = await authenticateControlPlaneCall(req, res);
  return authResult;
}

/**
 * Helper to handle job logging with state management
 * @param {object} req - Express request object
 * @param {string} jobInstanceId - Job instance ID
 */
async function handleCreateJob(req, jobInstanceId) {
  const operation = 'CREATE_JOB';
  try {    
    await appendToJobLog(req, operation);
    console.log(`✅[Backend] Successfully logged ${operation} for ${jobInstanceId}`);
    const existingState = getJobInstanceState(jobInstanceId);
    const completedState = {
      ...existingState,
      status: 'Completed',
      endTimeUtc: new Date().toISOString()
    };
    jobStates.set(jobInstanceId, completedState);
    console.log(`✅[Backend] Job ${jobInstanceId} completed successfully`);

  } catch (error) {
    console.error(`❌[Backend] Failed to log ${operation}: ${error.message}`);
    const existingState = getJobInstanceState(jobInstanceId);
    if (existingState) {
      const failedState = {
        ...existingState,
        status: 'Failed',
        endTimeUtc: new Date().toISOString(),
        errorDetails: {
          errorCode: 'OneLakeLoggingFailed',
          message: `Failed to log job to OneLake: ${error.message}`
        }
      };
      jobStates.set(jobInstanceId, failedState);
    }
  }
}

module.exports = router;