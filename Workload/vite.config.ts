import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

// Vite config for the FabInspector React workload.
// Dev server runs on 60006 to match the Fabric Extensibility Toolkit
// starter-kit convention (DevGateway registers this URL).
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), "");
    const backendUrl = env.VITE_BACKEND_URL ?? "https://localhost:7095";

    return {
        plugins: [react()],
        resolve: {
            alias: {
                "@app": path.resolve(__dirname, "app")
            }
        },
        server: {
            port: 60006,
            strictPort: true,
            // Forward `/api/workload/*` to the ASP.NET Core backend so the
            // React app can call lifecycle/job endpoints in dev without CORS.
            proxy: {
                "/api": {
                    target: backendUrl,
                    changeOrigin: true,
                    secure: false
                }
            }
        },
        build: {
            outDir: "../build/Frontend",
            emptyOutDir: true,
            sourcemap: true
        },
        test: {
            globals: true,
            environment: "jsdom",
            setupFiles: ["./app/test/setup.ts"]
        }
    };
});
