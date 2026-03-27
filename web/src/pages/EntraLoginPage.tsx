import { useCallback, useState } from "react";
import { identityProvider } from "../auth/IdentityProvider";

export default function EntraLoginPage() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleLogin = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await identityProvider.loginWithUsername("");
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Anmeldung fehlgeschlagen.";
      setError(message);
      setLoading(false);
    }
  }, []);

  return (
    <div className="login-shell">
      <div className="login-container">
        <div className="login-card">
          <div className="login-header">
            <img
              src="/mitarbeiter_lifecycle_icon.svg"
              alt="Mitarbeiter-Lifecycle"
              className="login-logo"
            />
            <h1>Mitarbeiter-Lifecycle</h1>
            <p className="login-subtitle">
              Bitte melden Sie sich mit Ihrem Unternehmenskonto an.
            </p>
          </div>

          <div className="login-body">
            {error && (
              <div className="login-error">
                <span>{error}</span>
              </div>
            )}

            <button
              type="button"
              className="btn login-button login-button-microsoft"
              onClick={handleLogin}
              disabled={loading}
            >
              <span className="login-button-microsoft-mark" aria-hidden="true">
                <span className="ms-tile ms-tile-red" />
                <span className="ms-tile ms-tile-green" />
                <span className="ms-tile ms-tile-blue" />
                <span className="ms-tile ms-tile-yellow" />
              </span>
              <span>{loading ? "Weiterleitung..." : "Mit Microsoft anmelden"}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
