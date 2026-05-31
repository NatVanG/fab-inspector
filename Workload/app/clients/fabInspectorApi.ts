import type { FabricItemController } from "@app/controller/fabricItemController";
import type { ItemDefinitionEnvelope } from "@app/types/itemDefinition";

/**
 * Typed wrappers around the .NET /api/workload/* endpoints exposed by
 * FabInspector.Web (ItemLifecycleController, JobActionController). The
 * backend is the source of truth for FabInspector item definitions —
 * the React editors never round-trip parts through the Fabric host.
 *
 * In Fabric-embedded mode the bearer is a frontend OBO token; in
 * standalone/dev mode the GET endpoints are anonymous and the POST
 * endpoints require a SubjectAndAppToken (only the Fabric host can mint
 * one), so "Run now" from inside the React editor is intentionally
 * unavailable outside the portal.
 */
const FABRIC_API_SCOPE = "https://api.fabric.microsoft.com/.default";

async function authHeaders(controller: FabricItemController): Promise<HeadersInit> {
    const token = await controller.acquireToken([FABRIC_API_SCOPE]);
    return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function getItemDefinition(
    controller: FabricItemController,
    itemType: string,
    workspaceId: string,
    itemId: string
): Promise<ItemDefinitionEnvelope | null> {
    const resp = await fetch(
        `/api/workload/items/${encodeURIComponent(itemType)}/${workspaceId}/${itemId}`,
        { headers: await authHeaders(controller) }
    );
    if (resp.status === 404) return null;
    if (!resp.ok) throw new Error(`GET item definition failed: ${resp.status} ${resp.statusText}`);
    return (await resp.json()) as ItemDefinitionEnvelope;
}

/**
 * PATCH the item definition. The .NET ItemLifecycleController accepts
 * an ItemDefinitionEnvelope and overlays its parts on the stored definition.
 */
export async function updateItemDefinition(
    controller: FabricItemController,
    itemType: string,
    workspaceId: string,
    itemId: string,
    envelope: ItemDefinitionEnvelope
): Promise<void> {
    const resp = await fetch(
        `/api/workload/items/${encodeURIComponent(itemType)}/${workspaceId}/${itemId}`,
        {
            method: "PATCH",
            headers: {
                ...(await authHeaders(controller)),
                "Content-Type": "application/json"
            },
            body: JSON.stringify(envelope)
        }
    );
    if (!resp.ok) {
        const detail = await resp.text().catch(() => "");
        throw new Error(`PATCH item definition failed: ${resp.status} ${resp.statusText} ${detail}`);
    }
}

export interface JobStatus {
    jobInstanceId: string;
    itemType: string;
    workspaceId: string;
    itemId: string;
    jobType: string;
    status: "Pending" | "InProgress" | "Succeeded" | "Failed" | "Cancelled" | string;
    startTime?: string;
    endTime?: string;
    failureMessage?: string;
    passCount?: number;
    failCount?: number;
    log?: string[];
}

export async function getJobStatus(
    controller: FabricItemController,
    itemType: string,
    workspaceId: string,
    itemId: string,
    jobType: string,
    jobInstanceId: string
): Promise<JobStatus | null> {
    const resp = await fetch(
        `/api/workload/jobs/${encodeURIComponent(itemType)}/${workspaceId}/${itemId}/${encodeURIComponent(jobType)}/${jobInstanceId}`,
        { headers: await authHeaders(controller) }
    );
    if (resp.status === 404) return null;
    if (!resp.ok) throw new Error(`GET job status failed: ${resp.status} ${resp.statusText}`);
    return (await resp.json()) as JobStatus;
}
