// Lokale Entwicklungssimulation ueber synchronisierte Entra-Benutzer.
import { useMemo, useState } from "react";
import { useAuth } from "../auth/useAuth";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";

export default function SimulationLoginPage() {
  const {
    simulationUsers,
    usersLoading,
    usersError,
    loginError,
    login,
    reloadUsers,
  } = useAuth();

  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState<boolean>(false);

  const preferredUserId = useMemo(
    () =>
      simulationUsers.find((user) => user.username.toLowerCase().includes("vinzent"))?.userId
      ?? simulationUsers[0]?.userId
      ?? null,
    [simulationUsers]
  );

  const resolvedSelectedUserId = useMemo(() => {
    if (selectedUserId && simulationUsers.some((user) => user.userId === selectedUserId)) {
      return selectedUserId;
    }

    return preferredUserId;
  }, [preferredUserId, selectedUserId, simulationUsers]);

  const selectedUser = useMemo(
    () => simulationUsers.find((user) => user.userId === resolvedSelectedUserId) ?? null,
    [resolvedSelectedUserId, simulationUsers]
  );

  const canSubmit = resolvedSelectedUserId !== null && !submitting;

  const handleLogin = async () => {
    if (!canSubmit || resolvedSelectedUserId === null) {
      return;
    }

    setSubmitting(true);
    await login(resolvedSelectedUserId);
    setSubmitting(false);
  };

  return (
    <div className="login-shell">
      <div className="login-container">
        <div className="login-header">
          <img
            className="login-logo"
            src="/mitarbeiter_lifecycle_icon.svg"
            alt="Kauth Mitarbeiterprozesse"
          />
          <h1>Kauth Mitarbeiterprozesse</h1>
          <p>Waehlen Sie einen synchronisierten Benutzer aus, um lokal dessen Rechte und Sicht zu simulieren.</p>
        </div>

        {usersLoading ? <LoadingState title="Simulationsbenutzer werden geladen..." /> : null}

        {!usersLoading && usersError ? (
          <EmptyState
            title="Simulationsbenutzer konnten nicht geladen werden."
            description={usersError}
            actionLabel="Erneut laden"
            onAction={() => {
              void reloadUsers();
            }}
          />
        ) : null}

        {!usersLoading && !usersError ? (
          <section className="panel">
            <div className="panel-head">
              <h2>Benutzer simulieren</h2>
              <p>Die Liste kommt aus der lokal synchronisierten Entra-Projektion.</p>
            </div>

            {simulationUsers.length === 0 ? (
              <EmptyState
                title="Keine synchronisierten Benutzer verfügbar"
                description="Pruefen Sie die Entra-Konfiguration, den Directory-Sync und die lokalen Gruppen-Mappings."
              />
            ) : (
              <>
                <div className="toolbar-row">
                  <label className="field compact grow">
                    <span>Benutzer</span>
                    <select
                      value={resolvedSelectedUserId ?? ""}
                      onChange={(event) => setSelectedUserId(Number(event.target.value))}
                    >
                      {simulationUsers.map((user) => (
                        <option key={user.userId} value={user.userId}>
                          {user.displayName} ({user.username})
                          {user.roleKeys.length > 0 ? ` – ${user.roleKeys.join(", ")}` : " – keine Rolle"}
                        </option>
                      ))}
                    </select>
                  </label>

                  <button type="button" className="btn btn-primary" disabled={!canSubmit} onClick={handleLogin}>
                    {submitting ? "Anmeldung..." : "Anmelden"}
                  </button>
                </div>

                {selectedUser ? (
                  <p className="panel-note">
                    E-Mail: {selectedUser.email}
                    {selectedUser.departmentName ? ` | Abteilung: ${selectedUser.departmentName}` : ""}
                    {selectedUser.roleKeys.length > 0
                      ? ` | Rollen: ${selectedUser.roleKeys.join(", ")}`
                      : " | Rollen: –"}
                  </p>
                ) : null}

                {loginError ? <p className="panel-text">{loginError}</p> : null}
              </>
            )}
          </section>
        ) : null}
      </div>
    </div>
  );
}
