import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import LoadingState from "../components/feedback/LoadingState";
import EmptyState from "../components/feedback/EmptyState";
import { identityProvider } from "../auth/IdentityProvider";
import { useAuth } from "../auth/useAuth";

export default function DemoAccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { refreshMe } = useAuth();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isActive = true;

    const run = async () => {
      const token = searchParams.get("token")?.trim();
      const redirect = searchParams.get("redirect")?.trim() || "/";

      if (!token) {
        setError("Der Demo-Zugangslink ist unvollständig.");
        return;
      }

      try {
        identityProvider.setStoredToken(token);
        await identityProvider.getCurrentUser();
        await refreshMe();

        if (isActive) {
          navigate(redirect, { replace: true });
        }
      } catch {
        identityProvider.setStoredToken(null);
        if (isActive) {
          setError("Der Demo-Zugangslink ist ungültig oder abgelaufen.");
        }
      }
    };

    void run();

    return () => {
      isActive = false;
    };
  }, [navigate, refreshMe, searchParams]);

  return (
    <main className="app-shell">
      <div className="page-container">
        {error ? (
          <EmptyState title="Demo-Zugang nicht möglich" description={error} />
        ) : (
          <LoadingState
            title="Demo-Zugang wird vorbereitet..."
            description="Sie werden als Empfänger der Benachrichtigung angemeldet."
          />
        )}
      </div>
    </main>
  );
}
