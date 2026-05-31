import { describe, expect, it } from "vitest";
import { decodePart, encodePart, findPart } from "@app/types/itemDefinition";

describe("itemDefinition encoding", () => {
    it("round-trips a UTF-8 payload through base64", () => {
        const original = '{"hello":"wörld","emoji":"🚀"}';
        const part = encodePart("rules.json", original);
        expect(part.path).toBe("rules.json");
        expect(part.payloadType).toBe("InlineBase64");
        expect(decodePart(part)).toBe(original);
    });

    it("findPart is case-insensitive on path", () => {
        const part = encodePart("rules.json", "{}");
        const envelope = { definition: { parts: [part] } };
        expect(findPart(envelope, "Rules.JSON")).toBe(part);
        expect(findPart(envelope, "missing.json")).toBeUndefined();
    });
});
