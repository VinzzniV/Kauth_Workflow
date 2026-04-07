// Startseite der Anwendung. Sie zeigt je nach Rolle den naechsten sinnvollen Einstiegspunkt.
import { Link } from "react-router-dom";
import { useCurrentUser } from "../auth/useCurrentUser";
import DashboardOverview from "../components/dashboard/DashboardOverview";
import PageHeader from "../components/layout/PageHeader";

export default function DashboardPage() {
  const { capabilities } = useCurrentUser();

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="section"
          title="Übersicht"
          description="Offene Vorgänge, aktive Aufgaben und nächste Schritte auf einen Blick."
          actions={
            capabilities.canCreateWorkflow ? (
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
