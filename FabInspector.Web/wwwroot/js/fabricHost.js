// Thin shim for the Microsoft Fabric Workload Client used by the
// FabInspector workload editor pages. When loaded inside the Fabric portal
// iframe, real workload-client APIs (itemCrud.updateItem, itemSchedule.runItemJob)
// are reachable via window.parent; when loaded standalone, these helpers
// degrade to console warnings so the page still works in browser-direct mode.
//
// For full SDK integration, replace this shim with @ms-fabric/workload-client.
window.fabricHost = window.fabricHost || {};

window.fabricHost.notifyContainerReady = function () {
    if (window.parent && window.parent !== window) {
        try {
            window.parent.postMessage(
                { type: "fabric.workload.containerReady", source: "FabInspector" },
                "*"
            );
        } catch (e) {
            console.warn("fabricHost.notifyContainerReady failed", e);
        }
    }
};

// Ask the Fabric host to persist the current item via itemCrud.updateItem.
// The payload is the serialized rules/catalog JSON; the host wraps it in an
// ItemDefinition.parts payload before forwarding to the Fabric BE.
window.fabricHost.updateItem = function (payload) {
    if (window.parent && window.parent !== window) {
        window.parent.postMessage(
            { type: "fabric.workload.itemCrud.update", source: "FabInspector", payload: payload },
            "*"
        );
    } else {
        console.warn("fabricHost.updateItem: not embedded; no-op");
    }
};

// Ask the Fabric host to start an item job via itemSchedule.runItemJob.
window.fabricHost.runItemJob = function (jobType) {
    if (window.parent && window.parent !== window) {
        window.parent.postMessage(
            { type: "fabric.workload.itemSchedule.runItemJob", source: "FabInspector", jobType: jobType },
            "*"
        );
    } else {
        console.warn("fabricHost.runItemJob: not embedded; no-op. jobType=", jobType);
    }
};

// Theme and context messages from the Fabric host.
window.addEventListener("message", function (event) {
    if (!event.data || typeof event.data !== "object") return;
    if (event.data.type === "fabric.workload.theme") {
        document.body.setAttribute("data-fabric-theme", event.data.theme || "light");
    }
});
