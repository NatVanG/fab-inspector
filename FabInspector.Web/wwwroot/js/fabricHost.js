// Minimal shim for the Microsoft Fabric Workload Client. When the page is loaded
// inside the Fabric portal iframe, the host posts an init message and expects
// the workload to call `notifyContainerReady`. When loaded standalone (browser
// directly), this file is a no-op.
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

// Listen for theme + context messages from the Fabric host.
window.addEventListener("message", function (event) {
    if (!event.data || typeof event.data !== "object") return;
    if (event.data.type === "fabric.workload.theme") {
        document.body.setAttribute("data-fabric-theme", event.data.theme || "light");
    }
});
