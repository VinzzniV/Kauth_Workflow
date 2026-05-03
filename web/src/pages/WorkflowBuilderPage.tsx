import { useCallback, useState } from "react";
import { AdminWorkflowBuilderFormSection } from "../components/admin-config/AdminWorkflowBuilderFormSection";
import { reportUserVisibleError } from "../services/systemLogReporter";

export default function WorkflowBuilderPage() {
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleError = useCallback((message: string | null) => {
    setError(message);

    if (!message) {
      return;
    }

    reportUserVisibleError({
      message,
      clientFunction: "WorkflowBuilderPage.setError",
      category: "ui",
      eventKey: "workflow_builder_error",
    });
  }, []);

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

        <AdminWorkflowBuilderFormSection
          onNotice={setNotice}
          onError={handleError}
        />
      </div>
    </main>
  );
}
