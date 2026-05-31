import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createWorkloadClient } from "@ms-fabric/workload-client";
import { App } from "@app/App";
import { FabricItemController } from "@app/controller/fabricItemController";

async function bootstrap() {
    let controller: FabricItemController;
    let client: ReturnType<typeof createWorkloadClient> | null = null;
    try {
        // createWorkloadClient handshakes with the Fabric host via postMessage.
        // It throws when loaded outside the portal — fall back to a no-op
        // controller so the app still renders for local debugging.
        client = createWorkloadClient();
        controller = new FabricItemController(client);
    } catch (err) {
        console.warn("createWorkloadClient failed; running in standalone mode", err);
        controller = new FabricItemController(null);
    }

    const rootEl = document.getElementById("root");
    if (!rootEl) throw new Error("Missing #root element");

    createRoot(rootEl).render(
        <StrictMode>
            <App controller={controller} client={client} />
        </StrictMode>
    );
}

void bootstrap();
