import { useEffect, useState } from "react";
import { bulkCreateDepartmentChange } from "../../services/adminConfigApi";
import { getRoles } from "../../services/lifecycleApi";
import type { AdminDepartmentAssignment } from "../../types/auth";
import type { BulkOperationResult, Role } from "../../types/workflow";

type AdminBulkOperationsSectionProps = {
  departments: AdminDepartmentAssignment[];
};

export function AdminBulkOperationsSection({
  departments,
}: AdminBulkOperationsSectionProps) {
  const [roles, setRoles] = useState<Role[]>([]);

  useEffect(() => {
    getRoles().then(setRoles).catch(() => setRoles([]));
  }, []);
  const [sourceDeptId, setSourceDeptId] = useState<string>("");
  const [targetDeptId, setTargetDeptId] = useState<string>("");
  const [targetRoleId, setTargetRoleId] = useState<string>("");
  const [deadlineDate, setDeadlineDate] = useState<string>("");
  const [isRunning, setIsRunning] = useState(false);
  const [result, setResult] = useState<BulkOperationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const filteredRoles = targetDeptId
    ? roles.filter((r) => r.departmentId === Number(targetDeptId))
    : roles;

  const canRun =
    sourceDeptId &&
    targetDeptId &&
    targetRoleId &&
    sourceDeptId !== targetDeptId &&
    !isRunning;

  const handleRun = async (dryRun: boolean) => {
    setIsRunning(true);
    setError(null);
    setResult(null);
    try {
      const res = await bulkCreateDepartmentChange({
        sourceDepartmentId: Number(sourceDeptId),
        targetDepartmentId: Number(targetDeptId),
        targetRoleId: Number(targetRoleId),
        deadlineDate: deadlineDate.trim() || null,
        dryRun,
      });
      setResult(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Fehler bei der Ausführung.");
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <>
      <section className="panel">
        <div className="panel-head">
          <h2>Massen-Abteilungswechsel</h2>
          <p>
            Erstellt für alle aktiven Mitarbeiter einer Quell-Abteilung automatisch einen
            Abteilungswechsel-Vorgang in die Ziel-Abteilung. Nutzen Sie die Vorschau, bevor Sie
            die Aktion ausführen.
          </p>
        </div>
        <div className="panel-body">
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", maxWidth: "40rem" }}>
            <label>
              <span className="form-label">Quell-Abteilung</span>
              <select className="form-select" value={sourceDeptId} onChange={(e) => setSourceDeptId(e.target.value)}>
                <option value="">-- Abteilung wählen --</option>
                {departments.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.departmentName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className="form-label">Ziel-Abteilung</span>
              <select className="form-select" value={targetDeptId} onChange={(e) => { setTargetDeptId(e.target.value); setTargetRoleId(""); }}>
                <option value="">-- Abteilung wählen --</option>
                {departments
                  .filter((d) => String(d.departmentId) !== sourceDeptId)
                  .map((d) => (
                    <option key={d.departmentId} value={d.departmentId}>
                      {d.departmentName}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              <span className="form-label">Ziel-Stelle</span>
              <select className="form-select" value={targetRoleId} onChange={(e) => setTargetRoleId(e.target.value)}>
                <option value="">-- Stelle wählen --</option>
                {filteredRoles.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className="form-label">Frist (optional)</span>
              <input
                className="form-input"
                type="date"
                value={deadlineDate}
                onChange={(e) => setDeadlineDate(e.target.value)}
              />
            </label>
          </div>

          <div style={{ marginTop: "1.25rem", display: "flex", gap: "0.75rem" }}>
            <button
              className="btn btn-outline"
              disabled={!canRun}
              onClick={() => void handleRun(true)}
            >
              {isRunning ? "Wird geprüft..." : "Vorschau (Dry Run)"}
            </button>
            <button
              className="btn btn-primary"
              disabled={!canRun}
              onClick={() => void handleRun(false)}
            >
              {isRunning ? "Wird ausgeführt..." : "Ausführen"}
            </button>
          </div>

          {error ? (
            <p className="text-error" style={{ marginTop: "1rem" }}>{error}</p>
          ) : null}
        </div>
      </section>

      {result ? (
        <section className="panel">
          <div className="panel-head">
            <h2>{result.isDryRun ? "Vorschau-Ergebnis" : "Ausführungsergebnis"}</h2>
            <p>
              {result.totalEmployees} Mitarbeiter gefunden.{" "}
              {result.isDryRun ? `${result.createdWorkflows} würden erstellt` : `${result.createdWorkflows} erstellt`},{" "}
              {result.skippedEmployees} übersprungen, {result.failedEmployees} fehlgeschlagen.
            </p>
          </div>
          <div className="panel-body">
            {result.items.length > 0 ? (
              <table className="table">
                <thead>
                  <tr>
                    <th>Person</th>
                    <th>Status</th>
                    <th>Vorgang</th>
                    <th>Hinweis</th>
                  </tr>
                </thead>
                <tbody>
                  {result.items.map((item) => (
                    <tr key={item.personId}>
                      <td>{item.displayName}</td>
                      <td>
                        <span
                          className={`badge badge--${
                            item.status === "created" || item.status === "would_create"
                              ? "success"
                              : item.status === "skipped"
                                ? "default"
                                : "error"
                          }`}
                        >
                          {item.status === "created"
                            ? "Erstellt"
                            : item.status === "would_create"
                              ? "Wird erstellt"
                              : item.status === "skipped"
                                ? "Übersprungen"
                                : "Fehler"}
                        </span>
                      </td>
                      <td>{item.workflowUid ? item.workflowUid.slice(0, 8) + "..." : "—"}</td>
                      <td className="text-muted">{item.errorMessage ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="text-muted">Keine Mitarbeiter in der Quell-Abteilung gefunden.</p>
            )}
          </div>
        </section>
      ) : null}
    </>
  );
}
