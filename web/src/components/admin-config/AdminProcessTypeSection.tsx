import { useEffect, useState } from "react";
import { getAdminProcessTypes, updateAdminProcessType } from "../../services/adminConfigApi";
import type { AdminProcessType } from "../../types/auth";

export function AdminProcessTypeSection() {
  const [processTypes, setProcessTypes] = useState<AdminProcessType[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editDraft, setEditDraft] = useState<{
    name: string;
    description: string;
    iconKey: string;
    sortOrder: string;
  }>({ name: "", description: "", iconKey: "", sortOrder: "" });
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = () => {
    setIsLoading(true);
    getAdminProcessTypes()
      .then(setProcessTypes)
      .catch(() => setProcessTypes([]))
      .finally(() => setIsLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const startEditing = (pt: AdminProcessType) => {
    setEditingId(pt.id);
    setEditDraft({
      name: pt.name,
      description: pt.description ?? "",
      iconKey: pt.iconKey ?? "",
      sortOrder: String(pt.sortOrder),
    });
    setError(null);
  };

  const cancelEditing = () => {
    setEditingId(null);
    setError(null);
  };

  const saveEditing = async () => {
    if (editingId === null) return;
    const current = processTypes.find((pt) => pt.id === editingId);
    if (!current) return;

    setIsSaving(true);
    setError(null);
    try {
      const payload: Record<string, string | number | null> = {};
      if (editDraft.name.trim() !== current.name) {
        payload.name = editDraft.name.trim();
      }
      const newDesc = editDraft.description.trim() || null;
      if (newDesc !== current.description) {
        payload.description = newDesc;
      }
      const newIcon = editDraft.iconKey.trim() || null;
      if (newIcon !== current.iconKey) {
        payload.iconKey = newIcon;
      }
      const newSort = Number(editDraft.sortOrder);
      if (!Number.isNaN(newSort) && newSort !== current.sortOrder) {
        payload.sortOrder = newSort;
      }

      if (Object.keys(payload).length === 0) {
        setEditingId(null);
        return;
      }

      await updateAdminProcessType(editingId, payload);
      load();
      setEditingId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Fehler beim Speichern.");
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActive = async (pt: AdminProcessType) => {
    if (!pt.isActive && !pt.canActivate) {
      setError(pt.activationBlockedReason ?? "Dieser Prozesstyp kann noch nicht aktiviert werden.");
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await updateAdminProcessType(pt.id, { isActive: !pt.isActive });
      load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Fehler beim Umschalten.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Prozesstypen</h2>
        <p>
          Alle konfigurierten Prozesstypen mit zugehörigen Anforderungen und Aufgabenvorlagen.
          Inaktive Prozesstypen werden bei der Workflow-Erstellung nicht angeboten.
        </p>
      </div>

      {isLoading ? (
        <p className="panel-note">Prozesstypen werden geladen...</p>
      ) : null}

      {!isLoading && processTypes.length === 0 ? (
        <p className="panel-note">Keine Prozesstypen vorhanden.</p>
      ) : null}

      {!isLoading && processTypes.length > 0 ? (
        <div className="panel-body">
          {error ? (
            <p className="text-error" style={{ marginBottom: "1rem" }}>{error}</p>
          ) : null}

          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Key</th>
                <th>Beschreibung</th>
                <th>Reihenfolge</th>
                <th>Anforderungen</th>
                <th>Aufgaben</th>
                <th>Vorgänge</th>
                <th>Optionen</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {processTypes.map((pt) => (
                <tr key={pt.id}>
                  {editingId === pt.id ? (
                    <>
                      <td>
                        <input
                          className="form-input"
                          value={editDraft.name}
                          onChange={(e) => setEditDraft({ ...editDraft, name: e.target.value })}
                          style={{ minWidth: "8rem" }}
                        />
                      </td>
                      <td className="text-muted">{pt.key}</td>
                      <td>
                        <input
                          className="form-input"
                          value={editDraft.description}
                          onChange={(e) => setEditDraft({ ...editDraft, description: e.target.value })}
                          style={{ minWidth: "12rem" }}
                        />
                      </td>
                      <td>
                        <input
                          className="form-input"
                          type="number"
                          value={editDraft.sortOrder}
                          onChange={(e) => setEditDraft({ ...editDraft, sortOrder: e.target.value })}
                          style={{ width: "4rem" }}
                        />
                      </td>
                      <td>{pt.answerDefinitionCount}</td>
                      <td>{pt.taskTemplateCount}</td>
                      <td>{pt.workflowCount}</td>
                      <td className="text-muted" style={{ fontSize: "0.85rem" }}>
                        {pt.requiresSupervisorStep ? "Vorgesetzten-Schritt" : null}
                        {pt.requiresSupervisorStep && pt.requiresTargetPerson ? ", " : null}
                        {pt.requiresTargetPerson ? "Zielperson" : null}
                        {!pt.requiresSupervisorStep && !pt.requiresTargetPerson ? "—" : null}
                      </td>
                      <td>
                        <span className={`badge badge--${pt.isActive ? "success" : "default"}`}>
                          {pt.isActive ? "Aktiv" : "Inaktiv"}
                        </span>
                      </td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        <button
                          className="btn btn-sm btn-primary"
                          disabled={isSaving}
                          onClick={() => void saveEditing()}
                        >
                          Speichern
                        </button>{" "}
                        <button
                          className="btn btn-sm btn-outline"
                          disabled={isSaving}
                          onClick={cancelEditing}
                        >
                          Abbrechen
                        </button>
                      </td>
                    </>
                  ) : (
                    <>
                      <td><strong>{pt.name}</strong></td>
                      <td className="text-muted">{pt.key}</td>
                      <td className="text-muted" style={{ maxWidth: "16rem" }}>{pt.description ?? "—"}</td>
                      <td>{pt.sortOrder}</td>
                      <td>{pt.answerDefinitionCount}</td>
                      <td>{pt.taskTemplateCount}</td>
                      <td>{pt.workflowCount}</td>
                      <td className="text-muted" style={{ fontSize: "0.85rem" }}>
                        {pt.requiresSupervisorStep ? "Vorgesetzten-Schritt" : null}
                        {pt.requiresSupervisorStep && pt.requiresTargetPerson ? ", " : null}
                        {pt.requiresTargetPerson ? "Zielperson" : null}
                        {!pt.requiresSupervisorStep && !pt.requiresTargetPerson ? "—" : null}
                        {!pt.isActive && !pt.canActivate && pt.activationBlockedReason ? (
                          <div style={{ marginTop: "0.35rem" }}>{pt.activationBlockedReason}</div>
                        ) : null}
                      </td>
                      <td>
                        <button
                          className={`badge badge--${pt.isActive ? "success" : "default"}`}
                          style={{ cursor: "pointer", border: "none", background: "none" }}
                          disabled={isSaving || (!pt.isActive && !pt.canActivate)}
                          onClick={() => void toggleActive(pt)}
                          title={
                            pt.isActive
                              ? "Deaktivieren"
                              : pt.canActivate
                                ? "Aktivieren"
                                : pt.activationBlockedReason ?? "Aktivierung derzeit blockiert"
                          }
                        >
                          <span className={`badge badge--${pt.isActive ? "success" : "default"}`}>
                            {pt.isActive ? "Aktiv" : "Inaktiv"}
                          </span>
                        </button>
                      </td>
                      <td>
                        <button
                          className="btn btn-sm btn-outline"
                          disabled={isSaving}
                          onClick={() => startEditing(pt)}
                        >
                          Bearbeiten
                        </button>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}
