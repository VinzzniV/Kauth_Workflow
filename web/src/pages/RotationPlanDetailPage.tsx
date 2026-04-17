import { useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import { useToast } from "../components/feedback/useToast";
import PageHeader from "../components/layout/PageHeader";
import RotationAuditLog from "../components/rotation/RotationAuditLog";
import RotationNotificationsPanel from "../components/rotation/RotationNotificationsPanel";
import {
  createRotationStation,
  deleteRotationStation,
  regenerateRotationGeneratedTasks,
  updateRotationStation,
} from "../services/rotationApi";
import { queryKeys } from "../services/queryKeys";
import {
  useRotationAuditLog,
  useRotationGeneratedTasks,
  useRotationNotifications,
  useRotationPlanDetail,
} from "../services/queries/rotationQueries";
import type {
  RotationGeneratedTask,
  RotationPlanStatus,
  RotationStation,
  RotationStationStatus,
  RotationStationUpsertPayload,
} from "../types/rotation";
import { formatDate, formatDateTime } from "../utils/dateFormat";

type StationFormState = {
  departmentId: string;
  startDate: string;
  endDate: string;
  orderIndex: string;
  location: string;
  notes: string;
  status: RotationStationStatus;
};

function createEmptyStationForm(nextOrderIndex = 0): StationFormState {
  return {
    departmentId: "",
    startDate: "",
    endDate: "",
    orderIndex: String(nextOrderIndex),
    location: "",
    notes: "",
    status: "planned",
  };
}

function buildStationForm(station: RotationStation): StationFormState {
  return {
    departmentId: String(station.departmentId),
    startDate: station.startDate,
    endDate: station.endDate,
    orderIndex: String(station.orderIndex),
    location: station.location ?? "",
    notes: station.notes ?? "",
    status: station.status,
  };
}

function normalizeStationPayload(form: StationFormState): RotationStationUpsertPayload {
  return {
    departmentId: Number(form.departmentId),
    startDate: form.startDate,
    endDate: form.endDate,
    orderIndex: Number(form.orderIndex),
    location: form.location.trim() || undefined,
    notes: form.notes.trim() || undefined,
    status: form.status,
  };
}

function getPlanStatusLabel(status: RotationPlanStatus): string {
  switch (status) {
    case "active":
      return "Aktiv";
    case "completed":
      return "Abgeschlossen";
    case "archived":
      return "Archiviert";
    default:
      return "Entwurf";
  }
}

function getPlanStatusPillClass(status: RotationPlanStatus): string {
  switch (status) {
    case "active":
      return "running";
    case "completed":
    case "archived":
      return "completed";
    default:
      return "open";
  }
}

function getStationStatusLabel(status: RotationStationStatus): string {
  switch (status) {
    case "active":
      return "Aktiv";
    case "completed":
      return "Abgeschlossen";
    case "cancelled":
      return "Abgebrochen";
    default:
      return "Geplant";
  }
}

function getGeneratedTaskStatusLabel(task: RotationGeneratedTask): string {
  switch (task.status) {
    case "in_progress":
      return "In Bearbeitung";
    case "completed":
      return "Erledigt";
    case "failed":
      return "Fehlgeschlagen";
    case "cancelled":
      return "Storniert";
    default:
      return "Offen";
  }
}

export default function RotationPlanDetailPage() {
  const { planId } = useParams<{ planId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();
  const numericPlanId = Number(planId);
  const planDetailQuery = useRotationPlanDetail(Number.isFinite(numericPlanId) ? numericPlanId : null, true);
  const generatedTasksQuery = useRotationGeneratedTasks(Number.isFinite(numericPlanId) ? numericPlanId : null, true);
  const auditLogQuery = useRotationAuditLog(Number.isFinite(numericPlanId) ? numericPlanId : null, 100, 0, true);
  const notificationsQuery = useRotationNotifications(Number.isFinite(numericPlanId) ? numericPlanId : null, 100, 0, true);

  const plan = planDetailQuery.data;
  const orderedStations = useMemo(
    () => [...(plan?.stations ?? [])].sort((left, right) => left.orderIndex - right.orderIndex),
    [plan?.stations]
  );

  const [editingStationId, setEditingStationId] = useState<number | null>(null);
  const [stationForm, setStationForm] = useState<StationFormState>(createEmptyStationForm());
  const [isSavingStation, setIsSavingStation] = useState(false);
  const [isRegenerating, setIsRegenerating] = useState(false);
  const [deletingStationId, setDeletingStationId] = useState<number | null>(null);

  if (!Number.isFinite(numericPlanId) || numericPlanId <= 0) {
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

  async function reloadPlanData() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.planDetail(numericPlanId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.generatedTasks(numericPlanId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.auditLog(numericPlanId, 100, 0) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.notifications(numericPlanId, 100, 0) }),
      plan?.personId
        ? queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(plan.personId) })
        : Promise.resolve(),
    ]);
  }

  function openCreateStationForm() {
    setEditingStationId(null);
    setStationForm(createEmptyStationForm(orderedStations.length));
  }

  function openEditStationForm(station: RotationStation) {
    setEditingStationId(station.id);
    setStationForm(buildStationForm(station));
  }

  function resetStationForm() {
    setEditingStationId(null);
    setStationForm(createEmptyStationForm(orderedStations.length));
  }

  async function handleSaveStation() {
    setIsSavingStation(true);
    try {
      const payload = normalizeStationPayload(stationForm);
      if (
        !Number.isFinite(payload.departmentId) ||
        payload.departmentId <= 0 ||
        !payload.startDate ||
        !payload.endDate ||
        !Number.isFinite(payload.orderIndex)
      ) {
        throw new Error("Bitte alle Pflichtfelder der Station korrekt ausfüllen.");
      }

      if (editingStationId) {
        await updateRotationStation(editingStationId, payload);
        showSuccess("Station wurde aktualisiert.");
      } else {
        await createRotationStation(numericPlanId, payload);
        showSuccess("Station wurde angelegt.");
      }

      await reloadPlanData();
      resetStationForm();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Station konnte nicht gespeichert werden.";
      showError(message);
    } finally {
      setIsSavingStation(false);
    }
  }

  async function handleDeleteStation(station: RotationStation) {
    setDeletingStationId(station.id);
    try {
      await deleteRotationStation(station.id);
      showSuccess("Station wurde gelöscht.");
      await reloadPlanData();
      if (editingStationId === station.id) {
        resetStationForm();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Station konnte nicht gelöscht werden.";
      showError(message);
    } finally {
      setDeletingStationId(null);
    }
  }

  async function handleRegenerateTasks() {
    setIsRegenerating(true);
    try {
      const result = await regenerateRotationGeneratedTasks(numericPlanId);
      await reloadPlanData();
      showSuccess(
        `Tasks synchronisiert: ${result.created} neu, ${result.updated} aktualisiert, ${result.cancelled} storniert.`
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : "Task-Synchronisierung ist fehlgeschlagen.";
      showError(message);
    } finally {
      setIsRegenerating(false);
    }
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
                    onClick={() => void handleRegenerateTasks()}
                    disabled={isRegenerating}
                  >
                    {isRegenerating ? "Synchronisiere..." : "Tasks neu synchronisieren"}
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
                  <dd>{plan.sourceWorkflowUid}</dd>
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

            <section className="panel panel-muted">
              <div className="panel-head">
                <h2>Stationsformular</h2>
                <p>Listen-/Timeline-nahe Erfassung: Reihenfolge, Zeitraum und Status stehen im Vordergrund.</p>
              </div>

              <div className="workflow-grid" aria-label="Stationsformular">
                <div className="dashboard-card card-primary">
                  <label className="field compact">
                    <span>Abteilungs-ID</span>
                    <input
                      type="number"
                      value={stationForm.departmentId}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, departmentId: event.target.value }))
                      }
                      placeholder="z. B. 3"
                    />
                  </label>
                  <label className="field compact">
                    <span>Startdatum</span>
                    <input
                      type="date"
                      value={stationForm.startDate}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, startDate: event.target.value }))
                      }
                    />
                  </label>
                  <label className="field compact">
                    <span>Enddatum</span>
                    <input
                      type="date"
                      value={stationForm.endDate}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, endDate: event.target.value }))
                      }
                    />
                  </label>
                  <label className="field compact">
                    <span>Reihenfolge</span>
                    <input
                      type="number"
                      value={stationForm.orderIndex}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, orderIndex: event.target.value }))
                      }
                    />
                  </label>
                  <label className="field compact">
                    <span>Status</span>
                    <select
                      value={stationForm.status}
                      onChange={(event) =>
                        setStationForm((current) => ({
                          ...current,
                          status: event.target.value as RotationStationStatus,
                        }))
                      }
                    >
                      <option value="planned">Geplant</option>
                      <option value="active">Aktiv</option>
                      <option value="completed">Abgeschlossen</option>
                      <option value="cancelled">Abgebrochen</option>
                    </select>
                  </label>
                  <label className="field compact">
                    <span>Ort</span>
                    <input
                      type="text"
                      value={stationForm.location}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, location: event.target.value }))
                      }
                      placeholder="optional"
                    />
                  </label>
                  <label className="field compact">
                    <span>Notizen</span>
                    <textarea
                      value={stationForm.notes}
                      onChange={(event) =>
                        setStationForm((current) => ({ ...current, notes: event.target.value }))
                      }
                      rows={4}
                      placeholder="Hinweise für HR oder den Fachbereich"
                    />
                  </label>

                  <div className="action-row">
                    <button
                      type="button"
                      className="btn btn-primary"
                      onClick={() => void handleSaveStation()}
                      disabled={isSavingStation}
                    >
                      {isSavingStation
                        ? "Speichere..."
                        : editingStationId
                          ? "Station aktualisieren"
                          : "Station anlegen"}
                    </button>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={resetStationForm}
                      disabled={isSavingStation}
                    >
                      Zurücksetzen
                    </button>
                  </div>
                </div>
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Stationen</h2>
                <p>Die Reihenfolge bleibt klar über `orderIndex`, Zeitraum und Status sichtbar.</p>
              </div>

              {orderedStations.length === 0 ? (
                <EmptyState
                  title="Noch keine Stationen vorhanden"
                  description="Legen Sie die erste Abteilungsphase über das Formular an."
                  actionLabel="Stationsformular öffnen"
                  onAction={openCreateStationForm}
                />
              ) : (
                <div className="workflow-grid" aria-label="Stationsliste">
                  {orderedStations.map((station) => (
                    <article key={station.id} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <h3>
                          {station.orderIndex + 1}. {station.departmentName}
                        </h3>
                        <span className="status-pill open">{getStationStatusLabel(station.status)}</span>
                      </div>
                      <dl className="workflow-meta">
                        <div>
                          <dt>Zeitraum</dt>
                          <dd>
                            {formatDate(station.startDate)} bis {formatDate(station.endDate)}
                          </dd>
                        </div>
                        <div>
                          <dt>Ort</dt>
                          <dd>{station.location ?? "-"}</dd>
                        </div>
                        <div>
                          <dt>Notizen</dt>
                          <dd>{station.notes ?? "-"}</dd>
                        </div>
                      </dl>

                      <div className="action-row">
                        <button
                          type="button"
                          className="btn btn-secondary"
                          onClick={() => openEditStationForm(station)}
                        >
                          Bearbeiten
                        </button>
                        <button
                          type="button"
                          className="btn btn-secondary"
                          onClick={() => void handleDeleteStation(station)}
                          disabled={deletingStationId === station.id}
                        >
                          {deletingStationId === station.id ? "Lösche..." : "Löschen"}
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>

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
