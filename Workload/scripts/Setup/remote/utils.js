/**
 * Shared utility functions for API implementations
 */

/**
 * Helper function to create standard error response
 */
function createErrorResponse(errorCode, message, source = 'System', isPermanent = false, moreDetails = []) {
  return {
    errorCode,
    message,
    source,
    isPermanent,
    moreDetails
  };
}

/**
 * Helper function to validate UUID format
 */
function isValidUUID(str) {
  const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  return uuidRegex.test(str);
}

/**
 * Helper function to log request details
 */
function logRequest(req, operation) {
  console.log(`[${operation}] ${req.method} ${req.path}`);
  if (req.body && Object.keys(req.body).length > 0) {
    console.log(`  Body: ${JSON.stringify(req.body)}`);
  }
}

module.exports = {
  createErrorResponse,
  isValidUUID,
  logRequest
};
