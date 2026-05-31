import type { WorkloadClientAPI, NotificationType } from "@ms-fabric/workload-client";

/**
 * Map our app-level severity strings to the SDK's NotificationType enum.
 * The enum is numeric (Error=2, Loading=3, Success=4, Warning=5, ...).
 */
function toNotificationType(level: "info" | "success" | "warn" | "error"): NotificationType {
    const map: Record<string, number> = {
        info: 3,    // Loading — closest neutral toast
        success: 4, // Success
        warn: 5,    // Warning
        error: 2    // Error
    };
    return map[level] as NotificationType;
}

/**
 * Thin wrapper around the @ms-fabric/workload-client APIs we actually use,
 * with graceful fallbacks when the app is loaded outside the Fabric portal
 * (standalone browser, unit tests). Replaces the hand-rolled postMessage
 * shim in FabInspector.Web/wwwroot/js/fabricHost.js.
 *
 * Item-definition read/write is intentionally NOT exposed here — the
 * .NET backend (ItemLifecycleController) is the source of truth for
 * FabInspector item parts. Use `clients/fabInspectorApi.ts` instead.
 */
export class FabricItemController {
    constructor(private readonly client: WorkloadClientAPI | null) {}

    get isEmbedded(): boolean {
        return this.client != null;
    }

    /** Trigger a Fabric scheduler run of the given job type for this item. */
    async runItemJob(itemObjectId: string, itemJobType: string): Promise<void> {
        if (!this.client) {
            console.warn("runItemJob: not embedded in Fabric host; no-op. itemJobType=", itemJobType);
            return;
        }
        await this.client.itemSchedule.runItemJob({
            itemObjectId,
            itemJobType,
            payload: { jobPayloadJson: "" }
        });
    }

    /** Surface a Fluent-style toast in the Fabric portal. */
    async notify(level: "info" | "success" | "warn" | "error", title: string, message?: string): Promise<void> {
        if (!this.client) {
            const out = `[${level}] ${title}${message ? ` — ${message}` : ""}`;
            // eslint-disable-next-line no-console
            (level === "error" ? console.error : console.log)(out);
            return;
        }
        try {
            await this.client.notification.open({
                notificationType: toNotificationType(level),
                title,
                message: message ?? ""
            });
        } catch (err) {
            console.warn("notification.open failed", err);
        }
    }

    /**
     * Acquire a delegated Microsoft Entra token (frontend OBO) for calling
     * Fabric / Power BI / OneLake / your own backend. Returns null in
     * standalone mode so callers can fall back to anonymous requests.
     */
    async acquireToken(scopes: string[] = []): Promise<string | null> {
        if (!this.client) return null;
        try {
            const token = await this.client.auth.acquireFrontendAccessToken({ scopes });
            return token?.token ?? null;
        } catch (err) {
            console.warn("acquireFrontendAccessToken failed", err);
            return null;
        }
    }

    /** Expose the underlying client for hooks that need direct API access (theme). */
    get raw(): WorkloadClientAPI | null {
        return this.client;
    }
}
