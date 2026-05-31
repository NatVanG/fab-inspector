import { createContext, useContext } from "react";
import type { FabricItemController } from "@app/controller/fabricItemController";

export const FabricControllerContext = createContext<FabricItemController | null>(null);

export function useFabricController(): FabricItemController {
    const c = useContext(FabricControllerContext);
    if (!c) throw new Error("FabricControllerContext is not initialised");
    return c;
}
