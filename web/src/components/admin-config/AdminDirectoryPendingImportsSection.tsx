import type { DirectoryPendingImports } from "../../types/auth";
import { useState, useRef } from "react";
import LoadingState from "../feedback/LoadingState";

type Props = {
  pendingImports: DirectoryPendingImports | null;
  isLoading: boolean;
  isImporting: boolean;
  onImport: (ids: number[]) => void | Promise<void>;
};

function roleKeyLabel(roleKey: string): string {
  switch (roleKey) {
    case "auth_manager": return "Abteilungsleitung";
    case "admin": return "Administrator";
    default: return roleKey;
  }
}

export function AdminDirectoryPendingImportsSection({ pendingImports, isLoading, isImporting, onImport }: Props) {
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const selectAllRef = useRef<HTMLInputElement | null>(null);

  const items = pendingImports?.pendingImports ?? [];
  const total = pendingImports?.totalCount ?? 0;

  if (!isLoading && total === 0) {
    return null;
  }

  const allSelected = items.length > 0 && selectedIds.size === items.length;
  const someSelected = selectedIds.size > 0 && selectedIds.size < items.length;

  if (selectAllRef.current) {
    selectAllRef.current.indeterminate = someSelected;
  }

  function toggleAll() {
    if (allSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(items.map((i) => i.directoryIdentityId)));
    }
  }

  function toggleOne(id: number) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function handleImport() {
    if (selectedIds.size === 0) return;
    void onImport(Array.from(selectedIds));
    setSelectedIds(new Set());
  }

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Ausstehende Importe</h2>
        {!isLoading && total > 0 ? (
          <span className="badge badge--default">{total}</span>
        ) : null}
      </div>

      <p className="panel-note">
        Diese Personen befinden sich in synchronisierten Entra-Gruppen, wurden aber noch nicht als
        Benutzer importiert. Wählen Sie Personen aus und klicken Sie auf "Importieren", um sie anzulegen.
      </p>

      {isLoading ? (
        <LoadingState title="Ausstehende Importe werden geladen..." />
      ) : (
        <>
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: "2.5rem" }}>
                  <input
                    ref={selectAllRef}
                    type="checkbox"
                    checked={allSelected}
                    onChange={toggleAll}
                    aria-label="Alle auswählen"
                    disabled={isImporting}
                  />
                </th>
                <th>Name</th>
                <th>E-Mail / UPN</th>
                <th>Entra-Gruppen</th>
                <th>→ Abteilung</th>
                <th>→ Rollen</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.directoryIdentityId}>
                  <td>
                    <input
                      type="checkbox"
                      checked={selectedIds.has(item.directoryIdentityId)}
                      onChange={() => toggleOne(item.directoryIdentityId)}
                      aria-label={`${item.displayName} auswählen`}
                      disabled={isImporting}
                    />
                  </td>
                  <td>{item.displayName}</td>
                  <td>
                    {item.mail ? (
                      <div>{item.mail}</div>
                    ) : null}
                    <div className="panel-note">{item.userPrincipalName}</div>
                  </td>
                  <td>
                    {item.groupNames.map((g) => (
                      <div key={g}>{g}</div>
                    ))}
                  </td>
                  <td>{item.departmentName ?? <span className="panel-note">Keine Abteilung</span>}</td>
                  <td>
                    {item.previewRoleKeys.length > 0
                      ? item.previewRoleKeys.map((r) => (
                          <div key={r}>
                            <span className="badge badge--default">{roleKeyLabel(r)}</span>
                          </div>
                        ))
                      : <span className="panel-note">–</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

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
          </div>
        </>
      )}
    </section>
  );
}
