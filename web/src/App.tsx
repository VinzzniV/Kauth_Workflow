// Zentrale App-Huelle fuer Auth-Status, Layout und geschuetzte Routen.
import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "./auth/useAuth";
import { useCurrentUser } from "./auth/useCurrentUser";
import LoadingState from "./components/feedback/LoadingState";
import AppLayout from "./components/layout/AppLayout";
import RouteGuard from "./navigation/RouteGuard";
import AdminConfigPage from "./pages/AdminConfigPage";
import CreateWorkflowPage from "./pages/CreateWorkflowPage";
import DashboardPage from "./pages/DashboardPage";
import DemoAccessPage from "./pages/DemoAccessPage";
import DemoLoginPage from "./pages/DemoLoginPage";
import MyTasksPage from "./pages/MyTasksPage";
import SupervisorStepPage from "./pages/SupervisorStepPage";
import WorkflowDetailPage from "./pages/WorkflowDetailPage";
import WorkflowListPage from "./pages/WorkflowListPage";
import WorkflowSearchPage from "./pages/WorkflowSearchPage";
import PersonWorkflowHistoryPage from "./pages/PersonWorkflowHistoryPage";

export default function App() {
  const { status } = useAuth();
  const { defaultRoute } = useCurrentUser();
  const location = useLocation();

  if (location.pathname === "/demo/access") {
    return (
      <Routes>
        <Route path="/demo/access" element={<DemoAccessPage />} />
      </Routes>
    );
  }

  // Solange die Session geprueft wird, rendert die App bewusst noch keine Fachroute.
  if (status === "loading") {
    return (
      <div className="login-shell">
        <div className="login-container">
          <LoadingState
            title="Benutzersession wird geladen..."
            description="Bitte warten Sie, während Ihre Anmeldung geprüft wird."
          />
        </div>
      </div>
    );
  }

  if (status === "unauthenticated") {
    return <DemoLoginPage />;
  }

  // Alle Fachseiten laufen innerhalb desselben Layouts und werden ueber Feature-Guards abgesichert.
  return (
    <AppLayout>
      <Routes>
        <Route
          path="/"
          element={
            <RouteGuard feature="dashboard">
              <DashboardPage />
            </RouteGuard>
          }
        />
        <Route
          path="/create"
          element={
            <RouteGuard feature="workflowCreate">
              <CreateWorkflowPage />
            </RouteGuard>
          }
        />
        <Route
          path="/supervisor"
          element={
            <RouteGuard feature="supervisorStep">
              <SupervisorStepPage />
            </RouteGuard>
          }
        />
        <Route
          path="/tasks/my"
          element={
            <RouteGuard feature="technicalTasks">
              <MyTasksPage />
            </RouteGuard>
          }
        />
        <Route
          path="/workflows"
          element={
            <RouteGuard feature="workflowOverview">
              <WorkflowListPage />
            </RouteGuard>
          }
        />
        <Route path="/workflows/completed" element={<Navigate to="/workflows" replace />} />
        <Route
          path="/workflows/:uid"
          element={
            <RouteGuard feature="workflowOverview">
              <WorkflowDetailPage />
            </RouteGuard>
          }
        />
        <Route
          path="/search"
          element={
            <RouteGuard feature="workflowSearch">
              <WorkflowSearchPage />
            </RouteGuard>
          }
        />
        <Route
          path="/people/:personId"
          element={
            <RouteGuard feature="workflowOverview">
              <PersonWorkflowHistoryPage />
            </RouteGuard>
          }
        />
        <Route
          path="/admin/config"
          element={
            <RouteGuard feature="adminConfig">
              <AdminConfigPage />
            </RouteGuard>
          }
        />
        <Route path="/login" element={<Navigate to={defaultRoute} replace />} />
        <Route path="*" element={<Navigate to={defaultRoute} replace />} />
      </Routes>
    </AppLayout>
  );
}
