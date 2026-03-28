// Startseite der Anwendung. Sie zeigt je nach Rolle den naechsten sinnvollen Einstiegspunkt.
import DashboardOverview from "../components/dashboard/DashboardOverview";
import PageHeader from "../components/layout/PageHeader";

export default function DashboardPage() {
  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader title="Übersicht" />

        <DashboardOverview />
      </div>
    </main>
  );
}
