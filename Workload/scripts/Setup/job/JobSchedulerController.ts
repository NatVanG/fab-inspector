import { ItemJobInstance, RunItemJobParams, WorkloadClientAPI } from "@ms-fabric/workload-client";

/**
 * This function runs an on-demand job for a given item.
 * It calls the 'itemSchedule.runItemJob' function from the WorkloadClientAPI.
 *
 * @param {WorkloadClientAPI} workloadClient - An instance of the WorkloadClientAPI.
 * @param {string} objectId - The ObjectId of the item to run the job for.
 * @param {string} jobType - The type of job to run (e.g., "Org.WorkloadSample.SampleWorkloadItem.ScheduledJob").
 * @param {string} [jobPayload] - Optional JSON payload to pass to the job.
 * @returns {Promise<ItemJobInstance>} - A promise that resolves to the job instance result.
 */
export async function callRunItemJob(
    workloadClient: WorkloadClientAPI,
    objectId: string,
    jobType: string,
    jobPayload?: string
): Promise<ItemJobInstance> {
    const params: RunItemJobParams = {
        itemObjectId: objectId,
        itemJobType: jobType,
        payload: { executionData: jobPayload }
    };

    console.log(`Call Run Item Job. objectId: ${objectId}, jobType: ${jobType}`);

    try {
        const result: ItemJobInstance = await workloadClient.itemSchedule.runItemJob(params);
        console.log(`Executed job id: ${result.itemJobInstanceId}`);
        return result;
    } catch (exception) {
        console.error(`Failed running item job for ${objectId}`, exception);
        throw exception;
    }
}