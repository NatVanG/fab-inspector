import { useCallback, useEffect, useState } from "react";
import {
    Button,
    Checkbox,
    Input,
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
import { Add20Regular, Delete20Regular } from "@fluentui/react-icons";
import { useFabricController } from "@app/controller/FabricControllerContext";
import { useFabricItemContext } from "@app/controller/useFabricItemContext";
import { getItemDefinition, updateItemDefinition } from "@app/clients/fabInspectorApi";
import {
    decodePart,
    encodePart,
    findPart,
    type ItemDefinitionEnvelope,
    type RulesCatalogPayload,
    type RulesCatalogPointer
} from "@app/types/itemDefinition";
import { WorkloadItemTypes, WorkloadJobs, WorkloadParts } from "@app/items/constants";

const useStyles = makeStyles({
    page: {
        display: "flex",
        flexDirection: "column",
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL
    },
    grid: {
        display: "grid",
        gridTemplateColumns: "1.5fr 2fr 2fr auto auto",
        gap: tokens.spacingHorizontalS,
        alignItems: "center"
    },
    gridHeader: {
        fontWeight: tokens.fontWeightSemibold
    }
});

const EMPTY_CATALOG: RulesCatalogPayload = { version: "1.0", name: "", ruleSets: [] };

export function CatalogEditor() {
    const styles = useStyles();
    const controller = useFabricController();
    const { workspaceObjectId, itemObjectId } = useFabricItemContext();

    const [catalog, setCatalog] = useState<RulesCatalogPayload>(EMPTY_CATALOG);
    const [status, setStatus] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [loaded, setLoaded] = useState(false);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            if (!itemObjectId || !workspaceObjectId) { setLoaded(true); return; }
            const envelope: ItemDefinitionEnvelope | null = await getItemDefinition(
                controller,
                WorkloadItemTypes.RulesCatalog,
                workspaceObjectId,
                itemObjectId
            );
            if (cancelled) return;
            const part = findPart(envelope, WorkloadParts.CatalogJson);
            if (part) {
                try {
                    setCatalog(JSON.parse(decodePart(part)) as RulesCatalogPayload);
                } catch (e) {
                    setStatus(`Existing catalog payload is invalid JSON: ${(e as Error).message}`);
                }
            }
            setLoaded(true);
        })().catch(err => {
            setStatus(`Failed to load: ${(err as Error).message}`);
            setLoaded(true);
        });
        return () => { cancelled = true; };
    }, [controller, workspaceObjectId, itemObjectId]);

    const updatePointer = (index: number, patch: Partial<RulesCatalogPointer>) => {
        setCatalog(prev => {
            const next = { ...prev, ruleSets: prev.ruleSets.slice() };
            next.ruleSets[index] = { ...next.ruleSets[index], ...patch };
            return next;
        });
    };

    const addPointer = () => setCatalog(prev => ({
        ...prev,
        ruleSets: [...prev.ruleSets, { displayName: "", workspaceId: "", itemId: "", disabled: false }]
    }));
    const removePointer = (index: number) => setCatalog(prev => ({
        ...prev,
        ruleSets: prev.ruleSets.filter((_, i) => i !== index)
    }));

    const save = useCallback(async () => {
        if (!itemObjectId || !workspaceObjectId) return;
        setBusy(true);
        setStatus(null);
        try {
            const json = JSON.stringify(catalog, null, 2);
            const envelope: ItemDefinitionEnvelope = {
                definition: { parts: [encodePart(WorkloadParts.CatalogJson, json)] }
            };
            await updateItemDefinition(
                controller,
                WorkloadItemTypes.RulesCatalog,
                workspaceObjectId,
                itemObjectId,
                envelope
            );
            await controller.notify("success", "Catalog saved");
            setStatus(`Saved at ${new Date().toISOString()}`);
        } catch (err) {
            await controller.notify("error", "Save failed", (err as Error).message);
            setStatus(`Save failed: ${(err as Error).message}`);
        } finally {
            setBusy(false);
        }
    }, [controller, workspaceObjectId, itemObjectId, catalog]);

    const runAsJob = useCallback(async () => {
        if (!itemObjectId) return;
        setBusy(true);
        try {
            await controller.runItemJob(itemObjectId, WorkloadJobs.RunCatalog);
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
                <Title2>FabInspector rules catalog</Title2>
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
            <Title2>FabInspector rules catalog</Title2>
            <Text size={200}>
                Workspace: <code>{workspaceObjectId}</code> · Item: <code>{itemObjectId}</code>
            </Text>

            <Toolbar>
                <ToolbarButton appearance="primary" disabled={busy || !loaded} onClick={save}>Save</ToolbarButton>
                <ToolbarDivider />
                <ToolbarButton disabled={busy} onClick={runAsJob}>Run all (as Fabric job)</ToolbarButton>
            </Toolbar>

            <div>
                <Text weight="semibold">Catalog name</Text>
                <Input
                    value={catalog.name}
                    onChange={(_, d) => setCatalog(prev => ({ ...prev, name: d.value }))}
                />
            </div>

            <div>
                <Text weight="semibold">Referenced rule sets</Text>
                {loaded ? (
                    <div className={styles.grid}>
                        <span className={styles.gridHeader}>Display name</span>
                        <span className={styles.gridHeader}>Workspace ID</span>
                        <span className={styles.gridHeader}>Item ID</span>
                        <span className={styles.gridHeader}>Disabled</span>
                        <span />
                        {catalog.ruleSets.map((p, i) => (
                            <Row key={i} pointer={p} onChange={patch => updatePointer(i, patch)} onRemove={() => removePointer(i)} />
                        ))}
                    </div>
                ) : (
                    <Spinner size="tiny" />
                )}
                <Button icon={<Add20Regular />} appearance="subtle" onClick={addPointer} style={{ marginTop: tokens.spacingVerticalS }}>
                    Add reference
                </Button>
            </div>

            {status && <Text>{status}</Text>}
            {busy && <Spinner size="tiny" label="Working…" />}
        </div>
    );
}

interface RowProps {
    pointer: RulesCatalogPointer;
    onChange: (patch: Partial<RulesCatalogPointer>) => void;
    onRemove: () => void;
}

function Row({ pointer, onChange, onRemove }: RowProps) {
    return (
        <>
            <Input value={pointer.displayName} onChange={(_, d) => onChange({ displayName: d.value })} />
            <Input value={pointer.workspaceId} onChange={(_, d) => onChange({ workspaceId: d.value })} />
            <Input value={pointer.itemId} onChange={(_, d) => onChange({ itemId: d.value })} />
            <Checkbox checked={pointer.disabled} onChange={(_, d) => onChange({ disabled: Boolean(d.checked) })} />
            <Button icon={<Delete20Regular />} appearance="subtle" aria-label="Remove" onClick={onRemove} />
        </>
    );
}

export default CatalogEditor;
