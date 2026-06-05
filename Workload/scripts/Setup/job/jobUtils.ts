import {
    ItemJobData,
    ItemJobActionResult,
    ItemJobDetailSection,
    ItemJobStatus,
} from '@ms-fabric/workload-client';
import { t } from "i18next";

export function getJobDetailsPane(jobData: ItemJobData): ItemJobActionResult {
    const jobDetailsSection: ItemJobDetailSection = 
    {
        title: 'Job Details',
        data: [
            {
                label: t("Job_Type"),
                value: jobData.itemJobType,
                type: 'text',
            },
            {
                label: t("Job_Status"),
                value: ItemJobStatus[jobData.status],
                type: 'text',
            },
            {
                label: t("Job_Start_Time_UTC"),
                value: jobData.jobStartTimeUtc?.toString(),
                type: 'text',
            },
            {
                label: t("Job_End_Time_UTC"),
                value: jobData.jobEndTimeUtc?.toString(),
                type: 'text',
            },
            {
                label: t("Job_Instance_ID"),
                value: jobData.itemJobInstanceId,
                type: 'text',
            }                    
        ]
    }

    const itemDetailsSection: ItemJobDetailSection = 
    {
        title: 'Item Details',
        data: [
            {
                label: t("Item_Type_Label"),
                value: 'Sample Workload Item',
                type: 'text',
            },
            {
                label: t("Item_Name_Label"),
                value: jobData.itemName,
                type: 'text',
            },
            {
                label: t("Item_ID_Label"),
                value: jobData.itemObjectId,
                type: 'text',
            },
            {
                label: t("Workspace_Name_Label"),
                value: jobData.workspaceName,
                type: 'text',
            },
            {
                label: t("Workspace_ID_Label"),
                value: jobData.workspaceObjectId,
                type: 'text',
            },
            // IMPORTANT: Use the following item(as is, keeping the label and type) to show the item editor link
            {
                label: 'Item Editor',
                value: 'Open',
                type: 'link',
            },                
        ]
    }

    return {
        isSuccess: true,
        data: {
            type: 'default',
            sections: [jobDetailsSection, itemDetailsSection],
        },
    }; 
}
