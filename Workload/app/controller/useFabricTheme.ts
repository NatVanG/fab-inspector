import { useEffect, useState } from "react";
import { webLightTheme, webDarkTheme, teamsHighContrastTheme, type Theme } from "@fluentui/react-components";
import type { WorkloadClientAPI, ThemeConfiguration } from "@ms-fabric/workload-client";

/**
 * Subscribe to the Fabric host theme bridge and translate to a Fluent UI
 * v9 theme object. Replaces the data-fabric-theme body-attribute hack in
 * the legacy fabricHost.js shim.
 */
export function useFabricTheme(client: WorkloadClientAPI | null): Theme {
    const [theme, setTheme] = useState<Theme>(webLightTheme);

    useEffect(() => {
        if (!client?.theme) return;

        const apply = (cfg: ThemeConfiguration | undefined) => {
            const key = (cfg?.colorScheme ?? cfg?.name ?? "light").toLowerCase();
            if (key.includes("high") && key.includes("contrast")) setTheme(teamsHighContrastTheme);
            else if (key.includes("dark")) setTheme(webDarkTheme);
            else setTheme(webLightTheme);
        };

        client.theme.get().then(apply).catch(() => apply(undefined));
        // onChange returns void in this SDK — no unsubscribe handle.
        client.theme.onChange(apply);
    }, [client]);

    return theme;
}
