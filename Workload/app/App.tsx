import { FluentProvider } from "@fluentui/react-components";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import type { WorkloadClientAPI } from "@ms-fabric/workload-client";
import type { FabricItemController } from "@app/controller/fabricItemController";
import { FabricControllerContext } from "@app/controller/FabricControllerContext";
import { useFabricTheme } from "@app/controller/useFabricTheme";
import { RuleSetEditor } from "@app/items/FabInspectorRuleSet/RuleSetEditor";
import { CatalogEditor } from "@app/items/FabInspectorRulesCatalog/CatalogEditor";

export interface AppProps {
    controller: FabricItemController;
    client: WorkloadClientAPI | null;
}

export function App({ controller, client }: AppProps) {
    const theme = useFabricTheme(client);

    return (
        <FabricControllerContext.Provider value={controller}>
            <FluentProvider theme={theme} style={{ minHeight: "100vh" }}>
                <BrowserRouter>
                    <Routes>
                        <Route path="/items/ruleset" element={<RuleSetEditor />} />
                        <Route path="/items/catalog" element={<CatalogEditor />} />
                        <Route path="*" element={<Navigate to="/items/ruleset" replace />} />
                    </Routes>
                </BrowserRouter>
            </FluentProvider>
        </FabricControllerContext.Provider>
    );
}
