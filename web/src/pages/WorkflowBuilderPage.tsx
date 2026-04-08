import { useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import { AdminWorkflowBuilderSection } from "../components/admin-config/AdminWorkflowBuilderSection";

export default function WorkflowBuilderPage() {
  const { capabilities } = useCurrentUser();
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canManageAdvanced = capabilities.canManageAdminConfiguration;

  return (
    <main className="app-shell">
      <div className="page-container builder-product-page">
        {notice ? (
          <section className="panel panel-success">
            <p className="panel-text">{notice}</p>
          </section>
        ) : null}

        {error ? (
          <section className="panel panel-error" role="alert">
            <p className="panel-text text-error">{error}</p>
          </section>
        ) : null}

        <AdminWorkflowBuilderSection
          onNotice={setNotice}
          onError={setError}
          pageModeLabel={canManageAdvanced ? "Admin-Modus" : "Bearbeitungsmodus"}
        />
      </div>
    </main>
  );
}
