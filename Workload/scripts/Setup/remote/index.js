/**
 * Remote Backend APIs for Microsoft Fabric Workload
 * Exports backend API routers and registration functions
 */

const crudApi = require('./itemCrudApi');
const jobsApi = require('./jobsApi');
const endpointResolutionApi = require('./endpointResolutionApi');
const permissionsApi = require('./permissionsApi');

/**
 * Register Workload CRUD APIs with an Express application
 * @param {object} app Express application
 */
function registerCrudApis(app) {
  console.log('*** Mounting Workload Item Lifecycle APIs ***');
  app.use('/', crudApi);
}

/**
 * Register Workload Jobs APIs with an Express application
 * @param {object} app Express application
 */
function registerJobsApis(app) {
  console.log('*** Mounting Workload Jobs APIs ***');
  app.use('/', jobsApi);
}

/**
 * Register Workload Endpoint Resolution API with an Express application
 * @param {object} app Express application
 */
function registerEndpointResolutionApi(app) {
  console.log('*** Mounting Endpoint Resolution API ***');
  app.use('/', endpointResolutionApi);
}

/**
 * Register Workload Permissions API with an Express application
 * @param {object} app Express application
 */
function registerPermissionsApi(app) {
  console.log('*** Mounting Permissions API ***');
  app.use('/', permissionsApi);
}

module.exports = {
  crudApi,
  jobsApi,
  endpointResolutionApi,
  permissionsApi,
  registerCrudApis,
  registerJobsApis,
  registerEndpointResolutionApi,
  registerPermissionsApi
};
