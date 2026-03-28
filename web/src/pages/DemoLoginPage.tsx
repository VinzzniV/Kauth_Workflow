// Einfache Demo-Anmeldung ueber hinterlegte Testbenutzer.
import { useMemo, useState } from "react";
import { useAuth } from "../auth/useAuth";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";

export default function DemoLoginPage() {
  const {
    demoUsers,
    usersLoading,
    usersError,
    loginError,
    login,
    reloadUsers,
  } = useAuth();

  const [selectedUsername, setSelectedUsername] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  const preferredDemoUsername = useMemo(
    () =>
      demoUsers.find((user) => user.username === "laura.romankewicz")?.username
      ?? demoUsers.find((user) => user.username === "tobias.lueck")?.username
      ?? demoUsers.find((user) => user.username === "vinzent.niederwieser")?.username
      ?? demoUsers.find((user) => user.username === "admin.demo")?.username
      ?? demoUsers[0]?.username
      ?? "",
    [demoUsers]
  );

  const resolvedSelectedUsername = useMemo(() => {
    if (selectedUsername && demoUsers.some((user) => user.username === selectedUsername)) {
      return selectedUsername;
    }

    return preferredDemoUsername;
  }, [demoUsers, preferredDemoUsername, selectedUsername]);

  // Vorbelegung spart in der Demo einen zusaetzlichen Schritt beim ersten Laden.
  const selectedUser = useMemo(
    () => demoUsers.find((user) => user.username === resolvedSelectedUsername) ?? null,
    [demoUsers, resolvedSelectedUsername]
  );

  const canSubmit = resolvedSelectedUsername.trim().length > 0 && !submitting;

  // Die Login-Funktion selbst liegt im AuthContext, die Seite steuert nur Auswahl und UI-Zustand.
  const handleLogin = async () => {
    if (!canSubmit) {
      return;
    }

    setSubmitting(true);
    await login(resolvedSelectedUsername);
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
          <p>Wählen Sie einen Benutzer aus, um sich in die Demo anzumelden.</p>
        </div>

        {usersLoading ? <LoadingState title="Benutzer werden geladen..." /> : null}

        {!usersLoading && usersError ? (
          <EmptyState
            title="Benutzer konnten nicht geladen werden."
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
              <h2>Benutzer auswählen</h2>
              <p>Der ausgewählte Benutzer wird für die aktuelle Anmeldung verwendet.</p>
            </div>

            {demoUsers.length === 0 ? (
              <EmptyState
                title="Keine Benutzer verfügbar"
                description="Bitte prüfen Sie die hinterlegten Benutzer."
              />
            ) : (
              <>
                <div className="toolbar-row">
                  <label className="field compact grow">
                    <span>Benutzer</span>
                    <select
                      value={resolvedSelectedUsername}
                      onChange={(event) => setSelectedUsername(event.target.value)}
                    >
                      {demoUsers.map((user) => (
                        <option key={user.userId} value={user.username}>
                          {user.displayName} ({user.username})
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
