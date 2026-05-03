// Zentrale App-Huelle fuer Auth-Status, Layout und geschuetzte Routen.
import { lazy, Suspense } from "react";
import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "./auth/useAuth";
import { useCurrentUser } from "./auth/useCurrentUser";
import { isEntraMode } from "./auth/IdentityProvider";
import { AppErrorBoundary } from "./components/feedback/AppErrorBoundary";
import LoadingState from "./components/feedback/LoadingState";
import AppLayout from "./components/layout/AppLayout";
import RouteGuard from "./navigation/RouteGuard";
import DashboardPage from "./pages/DashboardPage";
import EntraLoginPage from "./pages/EntraLoginPage";
import SimulationLoginPage from "./pages/SimulationLoginPage";

const CreateWorkflowPage = lazy(() => import("./pages/CreateWorkflowPage"));
const SupervisorStepPage = lazy(() => import("./pages/SupervisorStepPage"));
const MyTasksPage = lazy(() => import("./pages/MyTasksPage"));
const WorkflowListPage = lazy(() => import("./pages/WorkflowListPage"));
const WorkflowDetailPage = lazy(() => import("./pages/WorkflowDetailPage"));
const WorkflowSearchPage = lazy(() => import("./pages/WorkflowSearchPage"));
const PersonWorkflowHistoryPage = lazy(() => import("./pages/PersonWorkflowHistoryPage"));
const AdminConfigPage = lazy(() => import("./pages/AdminConfigPage"));
const WorkflowBuilderPage = lazy(() => import("./pages/WorkflowBuilderPage"));
const RotationPlanningPage = lazy(() => import("./pages/RotationPlanningPage"));
const RotationPlanDetailPage = lazy(() => import("./pages/RotationPlanDetailPage"));
const RotationOperationsPage = lazy(() => import("./pages/RotationOperationsPage"));
const RotationTaskDetailPage = lazy(() => import("./pages/RotationTaskDetailPage"));
const AdminRotationConfigPage = lazy(() => import("./pages/AdminRotationConfigPage"));

function RouteLoadingFallback() {
  return (
    <LoadingState
      title="Seite wird geladen..."
      description="Die angeforderte Ansicht wird vorbereitet."
    />
  );
}

function LazyRoute({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<RouteLoadingFallback />}>{children}</Suspense>;
}

export default function App() {
  const { status } = useAuth();
  const { defaultRoute } = useCurrentUser();

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
    return isEntraMode() ? <EntraLoginPage /> : <SimulationLoginPage />;
  }

  return (
    <AppLayout>
      <RoutesWithErrorBoundary defaultRoute={defaultRoute} />
    </AppLayout>
  );
}

function RoutesWithErrorBoundary({ defaultRoute }: { defaultRoute: string }) {
  const location = useLocation();
  return (
    <AppErrorBoundary scope={location.pathname} resetKey={location.pathname}>
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
              <LazyRoute>
                <CreateWorkflowPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/supervisor"
          element={
            <RouteGuard feature="supervisorStep">
              <LazyRoute>
                <SupervisorStepPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/tasks/my"
          element={
            <RouteGuard feature="technicalTasks">
              <LazyRoute>
                <MyTasksPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/workflows"
          element={
            <RouteGuard feature="workflowOverview">
              <LazyRoute>
                <WorkflowListPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route path="/workflows/completed" element={<Navigate to="/workflows" replace />} />
        <Route
          path="/workflows/:uid"
          element={
            <RouteGuard feature="workflowOverview">
              <LazyRoute>
                <WorkflowDetailPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/search"
          element={
            <RouteGuard feature="workflowSearch">
              <LazyRoute>
                <WorkflowSearchPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/people/:personId"
          element={
            <RouteGuard feature="workflowOverview">
              <LazyRoute>
                <PersonWorkflowHistoryPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/rotation"
          element={
            <RouteGuard feature="rotationPlanning">
              <LazyRoute>
                <RotationPlanningPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/rotation/plans/:planId"
          element={
            <RouteGuard feature="rotationPlanning">
              <LazyRoute>
                <RotationPlanDetailPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/rotation/operations"
          element={
            <RouteGuard feature="technicalTasks">
              <LazyRoute>
                <RotationOperationsPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/rotation/tasks/:taskRef"
          element={
            <RouteGuard feature="technicalTasks">
              <LazyRoute>
                <RotationTaskDetailPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/builder"
          element={
            <RouteGuard feature="workflowBuilder">
              <LazyRoute>
                <WorkflowBuilderPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/admin/config"
          element={
            <RouteGuard feature="adminConfig">
              <LazyRoute>
                <AdminConfigPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route
          path="/admin/rotation/config"
          element={
            <RouteGuard feature="adminConfig">
              <LazyRoute>
                <AdminRotationConfigPage />
              </LazyRoute>
            </RouteGuard>
          }
        />
        <Route path="/login" element={<Navigate to={defaultRoute} replace />} />
        <Route path="*" element={<Navigate to={defaultRoute} replace />} />
      </Routes>
    </AppErrorBoundary>
  );
}
