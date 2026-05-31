/**
 * TypeScript mirrors of FabInspector.Web/Workload/Contracts/*.cs.
 * Used by the React editors when reading/writing item definitions through
 * either the Fabric host (workloadClient.itemCrud) or the .NET backend.
 */

export interface ItemDefinitionPart {
    path: string;
    /** Always "InlineBase64" today. */
    payloadType: string;
    /** Base64-encoded UTF-8 JSON. */
    payload: string;
}

export interface ItemDefinition {
    parts: ItemDefinitionPart[];
}

export interface ItemDefinitionEnvelope {
    definition: ItemDefinition;
}

export interface RulesCatalogPointer {
    displayName: string;
    workspaceId: string;
    itemId: string;
    disabled: boolean;
}

export interface RulesCatalogPayload {
    /** Optional schema version per the toolkit backward-compat guidance. */
    version?: string;
    name: string;
    ruleSets: RulesCatalogPointer[];
}

/** Encode an arbitrary JSON-stringifiable payload as a single definition part. */
export function encodePart(path: string, payload: string): ItemDefinitionPart {
    return {
        path,
        payloadType: "InlineBase64",
        payload: btoa(unescape(encodeURIComponent(payload)))
    };
}

/** Decode a definition part back to its UTF-8 string contents. */
export function decodePart(part: ItemDefinitionPart): string {
    return decodeURIComponent(escape(atob(part.payload)));
}

export function findPart(envelope: ItemDefinitionEnvelope | null | undefined, path: string): ItemDefinitionPart | undefined {
    return envelope?.definition?.parts?.find(p => p.path.toLowerCase() === path.toLowerCase());
}
