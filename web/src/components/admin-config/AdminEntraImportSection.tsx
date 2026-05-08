import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getUnlinkedDirectoryIdentities, importPeopleFromDirectory } from "../../services/peopleApi";
import { queryKeys } from "../../services/queryKeys";
import type { ImportPeopleFromDirectoryResult, UnlinkedDirectoryIdentity } from "../../types/auth";
import LoadingState from "../feedback/LoadingState";

type DeptGroup = {
  dept: string | null;
  items: UnlinkedDirectoryIdentity[];
};

function groupByDepartment(identities: UnlinkedDirectoryIdentity[]): DeptGroup[] {
  const map = new Map<string | null, UnlinkedDirectoryIdentity[]>();
  for (const item of identities) {
    const key = item.departmentName ?? null;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  }
  const groups = Array.from(map.entries()).map(([dept, items]) => ({ dept, items }));
  groups.sort((a, b) => {
    if (a.dept === null && b.dept === null) return 0;
    if (a.dept === null) return 1;
    if (b.dept === null) return -1;
    return a.dept.localeCompare(b.dept, "de");
  });
  return groups;
}

type GroupCheckboxProps = {
  items: UnlinkedDirectoryIdentity[];
  selectedIds: Set<number>;
  disabled: boolean;
  onToggleGroup: (ids: number[], selectAll: boolean) => void;
};

function GroupSelectAllCheckbox({ items, selectedIds, disabled, onToggleGroup }: GroupCheckboxProps) {
  const ref = useRef<HTMLInputElement | null>(null);
  const ids = items.map((i) => i.directoryIdentityId);
  const selectedInGroup = ids.filter((id) => selectedIds.has(id)).length;
  const allSelected = selectedInGroup === ids.length && ids.length > 0;
  const someSelected = selectedInGroup > 0 && selectedInGroup < ids.length;

  useEffect(() => {
    if (ref.current) {
      ref.current.indeterminate = someSelected;
    }
  }, [someSelected]);

  return (
    <input
      ref={ref}
      type="checkbox"
      checked={allSelected}
      onChange={() => onToggleGroup(ids, !allSelected)}
      disabled={disabled || ids.length === 0}
      aria-label="Alle in dieser Abteilung auswählen"
    />
  );
}

type GlobalCheckboxProps = {
  allIds: number[];
  selectedIds: Set<number>;
  disabled: boolean;
  onToggleAll: (selectAll: boolean) => void;
};

function GlobalSelectAllCheckbox({ allIds, selectedIds, disabled, onToggleAll }: GlobalCheckboxProps) {
  const ref = useRef<HTMLInputElement | null>(null);
  const selectedCount = allIds.filter((id) => selectedIds.has(id)).length;
  const allSelected = selectedCount === allIds.length && allIds.length > 0;
  const someSelected = selectedCount > 0 && selectedCount < allIds.length;

  useEffect(() => {
    if (ref.current) {
      ref.current.indeterminate = someSelected;
    }
  }, [someSelected]);

  return (
    <input
      ref={ref}
      type="checkbox"
      checked={allSelected}
      onChange={() => onToggleAll(!allSelected)}
      disabled={disabled || allIds.length === 0}
      aria-label="Alle auswählen"
    />
  );
}

function ImportResultBanner({ result, onDismiss }: { result: ImportPeopleFromDirectoryResult; onDismiss: () => void }) {
  const created = result.results.filter((r) => r.outcome === "created");
  const linked = result.results.filter((r) => r.outcome === "linked");
  const skipped = result.results.filter((r) => r.outcome === "skipped");

  return (
    <div className="panel card-primary">
      <div className="panel-head">
        <h3>Import abgeschlossen</h3>
        <button type="button" className="btn btn-ghost btn-sm" onClick={onDismiss}>
          Schließen
        </button>
      </div>
      <ul className="panel-note" style={{ listStyle: "none", padding: 0, margin: 0 }}>
        {result.createdCount > 0 && (
          <li>
            <span className="badge badge--success">{result.createdCount} angelegt</span>
            {created.map((r) => (
              <span key={r.directoryIdentityId} style={{ marginLeft: "0.5rem" }}>
                {r.displayName}
              </span>
            ))}
          </li>
        )}
        {result.linkedCount > 0 && (
          <li style={{ marginTop: "0.25rem" }}>
            <span className="badge badge--default">{result.linkedCount} verknüpft</span>
            {linked.map((r) => (
              <span key={r.directoryIdentityId} style={{ marginLeft: "0.5rem" }}>
                {r.displayName}
              </span>
            ))}
          </li>
        )}
        {result.skippedCount > 0 && (
          <li style={{ marginTop: "0.25rem" }}>
            <span className="badge badge--warning">{result.skippedCount} übersprungen</span>
            {skipped.map((r) => (
              <span key={r.directoryIdentityId} style={{ marginLeft: "0.5rem" }}>
                {r.displayName}
                {r.skipReason ? ` (${r.skipReason})` : ""}
              </span>
            ))}
          </li>
        )}
      </ul>
    </div>
  );
}

export function AdminEntraImportSection() {
  const queryClient = useQueryClient();
  const [onlyEnabled, setOnlyEnabled] = useState(true);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastResult, setLastResult] = useState<ImportPeopleFromDirectoryResult | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.admin.unlinkedIdentities(onlyEnabled),
    queryFn: () => getUnlinkedDirectoryIdentities(onlyEnabled),
  });

  const mutation = useMutation({
    mutationFn: (ids: number[]) => importPeopleFromDirectory(ids),
    onSuccess: (result) => {
      setLastResult(result);
      setSelectedIds(new Set());
      void queryClient.invalidateQueries({ queryKey: queryKeys.admin.unlinkedIdentities(onlyEnabled) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.admin.unlinkedIdentities(!onlyEnabled) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.people.directory("", 0) });
    },
  });

  const identities = data?.items ?? [];
  const groups = useMemo(() => groupByDepartment(identities), [identities]);
  const allIds = useMemo(() => identities.map((i) => i.directoryIdentityId), [identities]);
  const isImporting = mutation.isPending;

  function handleToggleAll(selectAll: boolean) {
    setSelectedIds(selectAll ? new Set(allIds) : new Set());
  }

  function handleToggleGroup(ids: number[], selectAll: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const id of ids) {
        if (selectAll) next.add(id);
        else next.delete(id);
      }
      return next;
    });
  }

  function handleToggleOne(id: number) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function handleImport() {
    if (selectedIds.size === 0) return;
    mutation.mutate(Array.from(selectedIds));
  }

  return (
    <div className="content-stack">
      {lastResult ? (
        <ImportResultBanner result={lastResult} onDismiss={() => setLastResult(null)} />
      ) : null}

      <section className="panel">
        <div className="panel-head">
          <h2>Aus Entra importieren</h2>
          {!isLoading && data ? (
            <span className="badge badge--default">{data.total}</span>
          ) : null}
        </div>

        <p className="panel-note">
          Diese Entra-Mitarbeitenden haben noch keine Mitarbeiterkarte in kauth_workflow. Nach dem
          Import erhalten sie einen vollständigen Personendatensatz ohne Login-Konto — bestehende
          App-Zugänge werden dabei automatisch verknüpft.
        </p>

        <div className="admin-list-toolbar" style={{ marginBottom: "0.75rem" }}>
          <label className="field compact admin-list-toolbar-checkbox">
            <input
              type="checkbox"
              checked={!onlyEnabled}
              onChange={(e) => {
                setOnlyEnabled(!e.target.checked);
                setSelectedIds(new Set());
              }}
            />
            <span>Deaktivierte Entra-Konten anzeigen</span>
          </label>
        </div>

        {isLoading ? (
          <LoadingState title="Identitäten werden geladen..." />
        ) : isError ? (
          <p className="panel-note">Fehler beim Laden der Identitäten.</p>
        ) : identities.length === 0 ? (
          <p className="panel-note">
            {onlyEnabled
              ? "Keine aktiven Entra-Identitäten ohne Mitarbeiterkarte gefunden."
              : "Keine Entra-Identitäten ohne Mitarbeiterkarte gefunden."}
          </p>
        ) : (
          <>
            <div className="table-scroll">
              <table className="table admin-data-table">
                <thead>
                  <tr>
                    <th style={{ width: "2.5rem" }}>
                      <GlobalSelectAllCheckbox
                        allIds={allIds}
                        selectedIds={selectedIds}
                        disabled={isImporting}
                        onToggleAll={handleToggleAll}
                      />
                    </th>
                    <th>Name</th>
                    <th>Stelle</th>
                    <th>Konto</th>
                  </tr>
                </thead>
                <tbody>
                  {groups.map(({ dept, items }) => (
                    <>
                      <tr key={`dept-${dept ?? "__none__"}`} className="admin-data-row-group-header">
                        <td>
                          <GroupSelectAllCheckbox
                            items={items}
                            selectedIds={selectedIds}
                            disabled={isImporting}
                            onToggleGroup={handleToggleGroup}
                          />
                        </td>
                        <td colSpan={3}>
                          <strong>{dept ?? "Ohne Abteilung"}</strong>
                          <span className="panel-note" style={{ marginLeft: "0.5rem" }}>
                            {items.length} {items.length === 1 ? "Person" : "Personen"}
                          </span>
                        </td>
                      </tr>
                      {items.map((item) => (
                        <tr
                          key={item.directoryIdentityId}
                          className={`admin-data-row${selectedIds.has(item.directoryIdentityId) ? " admin-data-row--selected" : ""}`}
                          onClick={() => !isImporting && handleToggleOne(item.directoryIdentityId)}
                          style={{ cursor: isImporting ? "default" : "pointer" }}
                        >
                          <td onClick={(e) => e.stopPropagation()}>
                            <input
                              type="checkbox"
                              checked={selectedIds.has(item.directoryIdentityId)}
                              onChange={() => handleToggleOne(item.directoryIdentityId)}
                              aria-label={`${item.displayName} auswählen`}
                              disabled={isImporting}
                            />
                          </td>
                          <td>
                            <div className="admin-data-cell-strong">{item.displayName}</div>
                            {item.userPrincipalName ? (
                              <div className="panel-note">{item.userPrincipalName}</div>
                            ) : null}
                            {item.hasLinkedAppUser ? (
                              <div>
                                <span className="badge badge--default" style={{ fontSize: "0.7rem" }}>
                                  App-Konto verknüpfbar
                                </span>
                              </div>
                            ) : null}
                          </td>
                          <td>
                            {item.jobTitle ? (
                              item.jobTitle
                            ) : (
                              <span className="meta-empty">–</span>
                            )}
                          </td>
                          <td>
                            {item.accountEnabled ? (
                              <span className="badge badge--success">Aktiv</span>
                            ) : (
                              <span className="badge badge--default">Deaktiviert</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="action-row">
              <button
                type="button"
                className="btn btn-primary"
                disabled={selectedIds.size === 0 || isImporting}
                onClick={handleImport}
              >
                {isImporting
                  ? "Wird importiert..."
                  : selectedIds.size > 0
                    ? `${selectedIds.size} ${selectedIds.size === 1 ? "Person" : "Personen"} importieren`
                    : "Personen auswählen"}
              </button>
              {mutation.isError ? (
                <span className="panel-note" style={{ color: "var(--color-error, red)" }}>
                  Import fehlgeschlagen.
                </span>
              ) : null}
            </div>
          </>
        )}
      </section>
    </div>
  );
}
