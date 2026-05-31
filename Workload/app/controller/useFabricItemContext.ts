import { useSearchParams } from "react-router-dom";

/**
 * Pull the workspace/item context that Fabric injects into the editor
 * iframe URL. Mirrors the QueryHelpers.ParseQuery usage in the old
 * Blazor pages (RuleSetEditor.razor / CatalogEditor.razor).
 */
export interface FabricItemContext {
    workspaceObjectId: string | null;
    itemObjectId: string | null;
}

export function useFabricItemContext(): FabricItemContext {
    const [params] = useSearchParams();
    return {
        workspaceObjectId: params.get("workspaceObjectId"),
        itemObjectId: params.get("itemObjectId")
    };
}
