import { useMemo } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import RotationAuditLog from "../components/rotation/RotationAuditLog";
import RotationCalendarView from "../components/rotation/RotationCalendarView";
import RotationNotificationsPanel from "../components/rotation/RotationNotificationsPanel";
import RotationStationFormCard from "../components/rotation/RotationStationFormCard";
import RotationStationTimeline from "../components/rotation/RotationStationTimeline";
import {
  getGeneratedTaskStatusLabel,
  getPlanStatusLabel,
  getPlanStatusPillClass,
} from "../components/rotation/rotationLabels";
import { useRotationStationForm } from "../hooks/useRotationStationForm";
import {
  useRotationAuditLog,
  useRotationGeneratedTasks,
  useRotationNotifications,
  useRotationPlanDetail,
} from "../services/queries/rotationQueries";
import { useDepartments } from "../services/queries/roleQueries";
import { formatDate, formatDateTime } from "../utils/dateFormat";

export default function RotationPlanDetailPage() {
  const { planId } = useParams<{ planId: string }>();
  const navigate = useNavigate();
  const numericPlanId = Number(planId);
  const isValidPlanId = Number.isFinite(numericPlanId) && numericPlanId > 0;

  const planDetailQuery = useRotationPlanDetail(isValidPlanId ? numericPlanId : null, true);
  const generatedTasksQuery = useRotationGeneratedTasks(isValidPlanId ? numericPlanId : null, true);
  const auditLogQuery = useRotationAuditLog(isValidPlanId ? numericPlanId : null, 100, 0, true);
  const notificationsQuery = useRotationNotifications(isValidPlanId ? numericPlanId : null, 100, 0, true);

  const departmentsQuery = useDepartments();
  const departments = departmentsQuery.data ?? [];

  const plan = planDetailQuery.data;
  const orderedStations = useMemo(
    () => [...(plan?.stations ?? [])].sort((left, right) => left.orderIndex - right.orderIndex),
    [plan?.stations]
  );

  const stationForm = useRotationStationForm({
    numericPlanId: isValidPlanId ? numericPlanId : 0,
    personId: plan?.personId ?? null,
    orderedStationsCount: orderedStations.length,
  });

  if (!isValidPlanId) {
    return (
      <main className="app-shell">
        <div className="page-container">
          <EmptyState
            title="Ungültige Plan-ID"
            description="Der gewünschte Durchlaufplan konnte nicht aufgelöst werden."
            actionLabel="Zur Planung zurück"
            onAction={() => void navigate("/rotation")}
          />
        </div>
      </main>
    );
  }

  return (
    <main className="app-shell">
      <div className="page-container">
        <div className="page-back-row">
          <Link to="/rotation" className="btn btn-ghost">
            ← Zurück zur Durchlaufplanung
          </Link>
        </div>

        {planDetailQuery.isLoading ? <LoadingState title="Durchlaufplan wird geladen..." /> : null}

        {!planDetailQuery.isLoading && planDetailQuery.error ? (
          <EmptyState
            title="Durchlaufplan konnte nicht geladen werden."
            description={
              planDetailQuery.error instanceof Error
                ? planDetailQuery.error.message
                : "Die Planansicht ist fehlgeschlagen."
            }
            actionLabel="Erneut versuchen"
            onAction={() => void planDetailQuery.refetch()}
          />
        ) : null}

        {!planDetailQuery.isLoading && !planDetailQuery.error && plan ? (
          <>
            <PageHeader
              variant="detail"
              eyebrow="Durchlaufplan"
              title={plan.title}
              description={`${plan.displayName} · ${plan.departmentName ?? "ohne Abteilung"} · ${getPlanStatusLabel(plan.status)}`}
              actions={
                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => void stationForm.handleRegenerateTasks()}
                    disabled={stationForm.isRegenerating}
                  >
                    {stationForm.isRegenerating ? "Synchronisiere..." : "Tasks neu synchronisieren"}
                  </button>
                </div>
              }
            />

            <section className="panel">
              <div className="panel-head">
                <h2>Planübersicht</h2>
              </div>
              <dl className="workflow-meta">
                <div>
                  <dt>Status</dt>
                  <dd>
                    <span className={`status-pill ${getPlanStatusPillClass(plan.status)}`}>
                      {getPlanStatusLabel(plan.status)}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt>Person</dt>
                  <dd>{plan.displayName}</dd>
                </div>
                <div>
                  <dt>Quell-Onboarding</dt>
                  <dd title={plan.sourceWorkflowUid} className="uid-value">
                    {plan.sourceWorkflowUid.slice(0, 8)}…
                  </dd>
                </div>
                <div>
                  <dt>Erstellt</dt>
                  <dd>{formatDateTime(plan.createdAt)}</dd>
                </div>
                <div>
                  <dt>Zuletzt geändert</dt>
                  <dd>{formatDateTime(plan.updatedAt)}</dd>
                </div>
              </dl>
            </section>

            <RotationStationFormCard
              stationForm={stationForm.stationForm}
              setStationForm={stationForm.setStationForm}
              departments={departments}
              isDepartmentsLoading={departmentsQuery.isLoading}
              isSavingStation={stationForm.isSavingStation}
              editingStationId={stationForm.editingStationId}
              onSave={() => void stationForm.handleSaveStation()}
              onReset={stationForm.resetStationForm}
            />

            <section className="panel">
              <div className="panel-head">
                <h2>Stationen</h2>
                <p>Chronologische Reihenfolge der Abteilungsphasen im Durchlauf.</p>
              </div>
              <RotationStationTimeline
                stations={orderedStations}
                editingStationId={stationForm.editingStationId}
                deletingStationId={stationForm.deletingStationId}
                onEdit={stationForm.openEditStationForm}
                onDelete={stationForm.handleDeleteStation}
                onOpenCreate={stationForm.openCreateStationForm}
              />
            </section>

            <RotationCalendarView stations={orderedStations} />

            <section className="panel">
              <div className="panel-head">
                <h2>Generierte Maßnahmen</h2>
                <p>Vorschau der aktuell aus Stationen und Vorlagen abgeleiteten Aufgaben.</p>
              </div>

              {generatedTasksQuery.isLoading ? <LoadingState title="Generierte Aufgaben werden geladen..." /> : null}

              {!generatedTasksQuery.isLoading && generatedTasksQuery.error ? (
                <EmptyState
                  title="Generierte Aufgaben konnten nicht geladen werden."
                  description={
                    generatedTasksQuery.error instanceof Error
                      ? generatedTasksQuery.error.message
                      : "Die Maßnahmenvorschau ist fehlgeschlagen."
                  }
                  actionLabel="Erneut versuchen"
                  onAction={() => void generatedTasksQuery.refetch()}
                />
              ) : null}

              {!generatedTasksQuery.isLoading &&
              !generatedTasksQuery.error &&
              (generatedTasksQuery.data?.length ?? 0) === 0 ? (
                <p className="panel-note">Für den aktuellen Plan sind noch keine Aufgaben generiert.</p>
              ) : null}

              {!generatedTasksQuery.isLoading &&
              !generatedTasksQuery.error &&
              (generatedTasksQuery.data?.length ?? 0) > 0 ? (
                <div className="workflow-grid" aria-label="Generierte Aufgaben">
                  {(generatedTasksQuery.data ?? []).map((task) => (
                    <article key={task.id} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <h3>{task.title}</h3>
                        <span className="status-pill running">{getGeneratedTaskStatusLabel(task)}</span>
                      </div>
                      <dl className="workflow-meta">
                        <div>
                          <dt>Abteilung</dt>
                          <dd>{task.departmentName ?? "-"}</dd>
                        </div>
                        <div>
                          <dt>Trigger</dt>
                          <dd>{task.triggerType === "enter" ? "Eintritt" : "Austritt"}</dd>
                        </div>
                        <div>
                          <dt>Wechselbezug</dt>
                          <dd>{formatDate(task.anchorDate)}</dd>
                        </div>
                        <div>
                          <dt>Fällig</dt>
                          <dd>{formatDate(task.dueDate)}</dd>
                        </div>
                        <div>
                          <dt>Verantwortung</dt>
                          <dd>{task.responsibilityName ?? "-"}</dd>
                        </div>
                        <div>
                          <dt>Vorlage</dt>
                          <dd>{task.templateTitle ?? "-"}</dd>
                        </div>
                      </dl>
                    </article>
                  ))}
                </div>
              ) : null}
            </section>

            <RotationAuditLog
              entries={auditLogQuery.data ?? []}
              isLoading={auditLogQuery.isLoading}
              error={
                auditLogQuery.error instanceof Error
                  ? auditLogQuery.error.message
                  : auditLogQuery.error
                    ? "Historie konnte nicht geladen werden."
                    : null
              }
            />

            <RotationNotificationsPanel
              notifications={notificationsQuery.data ?? []}
              isLoading={notificationsQuery.isLoading}
              error={
                notificationsQuery.error instanceof Error
                  ? notificationsQuery.error.message
                  : notificationsQuery.error
                    ? "Benachrichtigungen konnten nicht geladen werden."
                    : null
              }
            />
          </>
        ) : null}
      </div>
    </main>
  );
}
