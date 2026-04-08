import { useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import { AdminWorkflowBuilderSection } from "../components/admin-config/AdminWorkflowBuilderSection";
import PageHeader from "../components/layout/PageHeader";

export default function WorkflowBuilderPage() {
  const { capabilities } = useCurrentUser();
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canManageAdvanced = capabilities.canManageAdminConfiguration;

  return (
    <main className="app-shell">
      <div className="page-container builder-product-page">
        <PageHeader
          eyebrow="Workflow Plattform"
          title="Workflow Builder"
          description="Definitionen, Drafts und fachliche Ablauflogik in einem canvas-first Workspace."
          actions={(
            <div className="builder-mode-badges">
              <span className={`badge badge--default ${canManageAdvanced ? "builder-mode-badge builder-mode-badge--advanced" : "builder-mode-badge"}`}>
                {canManageAdvanced ? "Admin Builder" : "Builder"}
              </span>
              {!canManageAdvanced ? (
                <span className="badge badge--default builder-mode-badge builder-mode-badge--locked">
                  Automation gesperrt
                </span>
              ) : null}
            </div>
          )}
        />

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

        <AdminWorkflowBuilderSection onNotice={setNotice} onError={setError} />
      </div>
    </main>
  );
}
