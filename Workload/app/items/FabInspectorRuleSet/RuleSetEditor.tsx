import { useCallback, useEffect, useState } from "react";
import Editor from "@monaco-editor/react";
import {
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    Spinner,
    Text,
    Title2,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
    makeStyles,
    tokens
} from "@fluentui/react-components";
import { useFabricController } from "@app/controller/FabricControllerContext";
import { useFabricItemContext } from "@app/controller/useFabricItemContext";
import { getItemDefinition, updateItemDefinition } from "@app/clients/fabInspectorApi";
import {
    decodePart,
    encodePart,
    findPart,
    type ItemDefinitionEnvelope
} from "@app/types/itemDefinition";
import { WorkloadItemTypes, WorkloadJobs, WorkloadParts } from "@app/items/constants";

const useStyles = makeStyles({
    page: {
        display: "flex",
        flexDirection: "column",
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL
    },
    editor: {
        height: "60vh",
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        overflow: "hidden"
    },
    statusRow: {
        display: "flex",
        alignItems: "center",
        gap: tokens.spacingHorizontalS
    }
});

const DEFAULT_RULES = `{
  "rules": []
}
`;

export function RuleSetEditor() {
    const styles = useStyles();
    const controller = useFabricController();
    const { workspaceObjectId, itemObjectId } = useFabricItemContext();

    const [rulesJson, setRulesJson] = useState<string>(DEFAULT_RULES);
    const [validation, setValidation] = useState<{ ok: boolean; message: string } | null>(null);
    const [status, setStatus] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [loaded, setLoaded] = useState(false);

    // Hydrate from the .NET backend (source of truth).
    useEffect(() => {
        let cancelled = false;
        (async () => {
            if (!itemObjectId || !workspaceObjectId) {
                setLoaded(true);
                return;
            }
            const envelope: ItemDefinitionEnvelope | null = await getItemDefinition(
                controller,
                WorkloadItemTypes.RuleSet,
                workspaceObjectId,
                itemObjectId
            );
            if (cancelled) return;
            const part = findPart(envelope, WorkloadParts.RulesJson);
            if (part) setRulesJson(decodePart(part));
            setLoaded(true);
        })().catch(err => {
            console.error("Failed to load rule set", err);
            setStatus(`Failed to load: ${(err as Error).message}`);
            setLoaded(true);
        });
        return () => { cancelled = true; };
    }, [controller, workspaceObjectId, itemObjectId]);

    const validate = useCallback((): boolean => {
        try {
            JSON.parse(rulesJson);
            setValidation({ ok: true, message: "JSON is valid." });
            return true;
        } catch (e) {
            setValidation({ ok: false, message: `Invalid JSON: ${(e as Error).message}` });
            return false;
        }
    }, [rulesJson]);

    const save = useCallback(async () => {
        if (!itemObjectId || !workspaceObjectId) return;
        if (!validate()) return;
        setBusy(true);
        setStatus(null);
        try {
            const envelope: ItemDefinitionEnvelope = {
                definition: { parts: [encodePart(WorkloadParts.RulesJson, rulesJson)] }
            };
            await updateItemDefinition(
                controller,
                WorkloadItemTypes.RuleSet,
                workspaceObjectId,
                itemObjectId,
                envelope
            );
            await controller.notify("success", "Rule set saved");
            setStatus(`Saved at ${new Date().toISOString()}`);
        } catch (err) {
            await controller.notify("error", "Save failed", (err as Error).message);
            setStatus(`Save failed: ${(err as Error).message}`);
        } finally {
            setBusy(false);
        }
    }, [controller, workspaceObjectId, itemObjectId, rulesJson, validate]);

    const runAsJob = useCallback(async () => {
        if (!itemObjectId) return;
        setBusy(true);
        try {
            await controller.runItemJob(itemObjectId, WorkloadJobs.RunRules);
            setStatus("Requested Fabric host to start job. Track progress in Recent runs.");
        } catch (err) {
            await controller.notify("error", "Could not start job", (err as Error).message);
            setStatus(`Could not start job: ${(err as Error).message}`);
        } finally {
            setBusy(false);
        }
    }, [controller, itemObjectId]);

    if (!workspaceObjectId || !itemObjectId) {
        return (
            <div className={styles.page}>
                <Title2>FabInspector rule set</Title2>
                <MessageBar intent="warning">
                    <MessageBarBody>
                        <MessageBarTitle>Missing context</MessageBarTitle>
                        Launch this editor from inside Fabric — workspace and item IDs must be supplied via query string.
                    </MessageBarBody>
                </MessageBar>
            </div>
        );
    }

    return (
        <div className={styles.page}>
            <Title2>FabInspector rule set</Title2>
            <Text size={200}>
                Workspace: <code>{workspaceObjectId}</code> · Item: <code>{itemObjectId}</code>
            </Text>

            <Toolbar>
                <ToolbarButton appearance="primary" disabled={busy || !loaded} onClick={save}>Save</ToolbarButton>
                <ToolbarButton disabled={busy} onClick={validate}>Validate JSON</ToolbarButton>
                <ToolbarDivider />
                <ToolbarButton disabled={busy} onClick={runAsJob}>Run as Fabric job</ToolbarButton>
            </Toolbar>

            {validation && (
                <MessageBar intent={validation.ok ? "success" : "error"}>
                    <MessageBarBody>{validation.message}</MessageBarBody>
                </MessageBar>
            )}

            <div className={styles.editor}>
                {loaded ? (
                    <Editor
                        defaultLanguage="json"
                        value={rulesJson}
                        onChange={value => setRulesJson(value ?? "")}
                        options={{
                            minimap: { enabled: false },
                            scrollBeyondLastLine: false,
                            tabSize: 2,
                            fontSize: 13
                        }}
                    />
                ) : (
                    <div className={styles.statusRow}><Spinner size="tiny" /> <Text>Loading…</Text></div>
                )}
            </div>

            {status && <Text>{status}</Text>}
            {busy && <Spinner size="tiny" label="Working…" />}
        </div>
    );
}

export default RuleSetEditor;
