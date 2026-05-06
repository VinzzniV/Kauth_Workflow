// Startseite der Anwendung. Sie zeigt je nach Rolle den naechsten sinnvollen Einstiegspunkt.
import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import DashboardOverview from "../components/dashboard/DashboardOverview";
import PageHeader from "../components/layout/PageHeader";
import { useRoleAwareNavigation } from "../navigation/useRoleAwareNavigation";

export default function DashboardPage() {
  const { capabilities } = useCurrentUser();
  const { dashboardPersona } = useRoleAwareNavigation();

  const pageDescription =
    dashboardPersona === "admin"
      ? "Governance, Betriebsstatus und operative Risiken der Administration auf einen Blick."
      : "Offene Vorgänge, aktive Aufgaben und nächste Schritte auf einen Blick.";

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="section"
          title="Übersicht"
          description={pageDescription}
          actions={
            capabilities.canCreateWorkflow && dashboardPersona !== "admin" ? (
              <Link to="/create" className="btn btn-primary">
                Neuen Vorgang anlegen
              </Link>
            ) : undefined
          }
        />

        <DashboardOverview />
      </div>
    </main>
  );
}
