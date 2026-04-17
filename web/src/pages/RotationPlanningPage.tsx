import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import { useToast } from "../components/feedback/useToast";
import PageHeader from "../components/layout/PageHeader";
import {
  createRotationPlan,
} from "../services/rotationApi";
import { queryKeys } from "../services/queryKeys";
import { useRotationCompletedOnboardings, useRotationPlans } from "../services/queries/rotationQueries";
import type {
  CompletedOnboardingSearchResult,
  RotationPlanStatus,
} from "../types/rotation";
import { formatDateTime } from "../utils/dateFormat";

function getRotationPlanStatusLabel(status: RotationPlanStatus): string {
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

function getRotationPlanStatusPillClass(status: RotationPlanStatus): string {
  switch (status) {
    case "completed":
    case "archived":
      return "completed";
    case "active":
      return "running";
    default:
      return "open";
  }
}

export default function RotationPlanningPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();
  const [search, setSearch] = useState("");
  const [selectedPerson, setSelectedPerson] = useState<CompletedOnboardingSearchResult | null>(null);
  const [planTitle, setPlanTitle] = useState("");
  const [planStatus, setPlanStatus] = useState<RotationPlanStatus>("draft");
  const [isCreatingPlan, setIsCreatingPlan] = useState(false);

  const completedOnboardingsQuery = useRotationCompletedOnboardings(search, true);
  const rotationPlansQuery = useRotationPlans(selectedPerson?.personId ?? null, Boolean(selectedPerson));

  const completedOnboardings = completedOnboardingsQuery.data;
  const existingPlans = rotationPlansQuery.data ?? [];
  const selectedPersonName = selectedPerson?.displayName ?? "Person";

  const latestCompletedOnboarding = useMemo(() => {
    if (!selectedPerson) {
      return null;
    }

    return (completedOnboardings ?? [])
      .filter((entry) => entry.personId === selectedPerson.personId)
      .sort((left, right) => new Date(right.completedAt).getTime() - new Date(left.completedAt).getTime())[0] ?? selectedPerson;
  }, [completedOnboardings, selectedPerson]);

  async function handleCreatePlan() {
    if (!selectedPerson) {
      showError("Bitte zuerst eine Person mit abgeschlossenem Onboarding auswählen.");
      return;
    }

    setIsCreatingPlan(true);
    try {
      const createdPlan = await createRotationPlan({
        personId: selectedPerson.personId,
        sourceWorkflowUid: selectedPerson.workflowUid,
        title: planTitle.trim() || undefined,
        status: planStatus,
      });

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(selectedPerson.personId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.rotation.completedOnboardings(search) }),
      ]);

      showSuccess("Durchlaufplan wurde angelegt.");
      void navigate(`/rotation/plans/${createdPlan.id}`);
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Durchlaufplan konnte nicht angelegt werden.";
      showError(message);
    } finally {
      setIsCreatingPlan(false);
    }
  }

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Durchlaufplanung"
          description="HR plant hier Durchläufe auf Basis bereits abgeschlossener Onboardings und steigt danach in den Stationsplan ein."
        />

        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>Person mit abgeschlossenem Onboarding suchen</h2>
            <p>Die Suche nutzt nur bestehende Personen, deren Onboarding bereits abgeschlossen wurde.</p>
          </div>

          <div className="toolbar-row toolbar-row-filters">
            <label className="field compact grow">
              <span>Suche</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Name, Personalnummer oder Ausweisnummer"
              />
            </label>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => void completedOnboardingsQuery.refetch()}
              disabled={completedOnboardingsQuery.isFetching}
            >
              {completedOnboardingsQuery.isFetching ? "Aktualisiere..." : "Aktualisieren"}
            </button>
          </div>
        </section>

        {completedOnboardingsQuery.isLoading ? (
          <LoadingState title="Abgeschlossene Onboardings werden geladen..." />
        ) : null}

        {!completedOnboardingsQuery.isLoading && completedOnboardingsQuery.error ? (
          <EmptyState
            title="Onboardings konnten nicht geladen werden."
            description={
              completedOnboardingsQuery.error instanceof Error
                ? completedOnboardingsQuery.error.message
                : "Die Suche ist fehlgeschlagen."
            }
            actionLabel="Erneut versuchen"
            onAction={() => void completedOnboardingsQuery.refetch()}
          />
        ) : null}

        {!completedOnboardingsQuery.isLoading &&
        !completedOnboardingsQuery.error &&
        (completedOnboardings?.length ?? 0) > 0 ? (
          <section className="workflow-grid" aria-label="Abgeschlossene Onboardings">
            {(completedOnboardings ?? []).map((entry) => {
              const isSelected = selectedPerson?.workflowUid === entry.workflowUid;
              return (
                <article key={entry.workflowUid} className="workflow-card card-list">
                  <div className="workflow-card-top">
                    <h2>{entry.displayName}</h2>
                    <span className={`status-pill ${isSelected ? "running" : "open"}`}>
                      {isSelected ? "Ausgewählt" : "Verfügbar"}
                    </span>
                  </div>
                  <dl className="workflow-meta">
                    <div>
                      <dt>Abteilung</dt>
                      <dd>{entry.departmentName ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>Rolle</dt>
                      <dd>{entry.roleName ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>Personalnummer</dt>
                      <dd>{entry.employeeNumber}</dd>
                    </div>
                    <div>
                      <dt>Abgeschlossen</dt>
                      <dd>{formatDateTime(entry.completedAt)}</dd>
                    </div>
                  </dl>
                  <div className="action-row">
                    <button
                      type="button"
                      className="btn btn-primary"
                      onClick={() => {
                        setSelectedPerson(entry);
                        setPlanTitle("");
                        setPlanStatus("draft");
                      }}
                    >
                      {isSelected ? "Ausgewählt" : "Person öffnen"}
                    </button>
                  </div>
                </article>
              );
            })}
          </section>
        ) : null}

        {!completedOnboardingsQuery.isLoading &&
        !completedOnboardingsQuery.error &&
        (completedOnboardings?.length ?? 0) === 0 ? (
          <EmptyState
            title="Keine abgeschlossenen Onboardings gefunden"
            description="Passen Sie die Suche an oder prüfen Sie, ob bereits Onboardings abgeschlossen wurden."
          />
        ) : null}

        {selectedPerson ? (
          <>
            <section className="panel">
              <div className="panel-head">
                <h2>Personendetail</h2>
                <p>Der Durchlaufplan startet immer aus einem abgeschlossenen Onboarding derselben Person.</p>
              </div>

              <dl className="workflow-meta">
                <div>
                  <dt>Name</dt>
                  <dd>{selectedPersonName}</dd>
                </div>
                <div>
                  <dt>Abteilung</dt>
                  <dd>{latestCompletedOnboarding?.departmentName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Rolle</dt>
                  <dd>{latestCompletedOnboarding?.roleName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Quell-Onboarding</dt>
                  <dd>{latestCompletedOnboarding?.workflowUid ?? "-"}</dd>
                </div>
                <div>
                  <dt>Abgeschlossen</dt>
                  <dd>{formatDateTime(latestCompletedOnboarding?.completedAt)}</dd>
                </div>
              </dl>

              <div className="action-row">
                <Link className="btn btn-secondary" to={`/people/${selectedPerson.personId}`}>
                  Mitarbeiterakte öffnen
                </Link>
              </div>
            </section>

            <section className="panel panel-muted">
              <div className="panel-head">
                <h2>Neuen Durchlaufplan anlegen</h2>
                <p>Ein Plan kann direkt als Entwurf oder aktiv gestartet werden.</p>
              </div>

              <div className="workflow-grid" aria-label="Plananlage">
                <div className="dashboard-card card-primary">
                  <label className="field compact">
                    <span>Titel</span>
                    <input
                      type="text"
                      value={planTitle}
                      onChange={(event) => setPlanTitle(event.target.value)}
                      placeholder={`${selectedPersonName} - Durchlaufplan`}
                    />
                  </label>

                  <label className="field compact">
                    <span>Status</span>
                    <select
                      value={planStatus}
                      onChange={(event) => setPlanStatus(event.target.value as RotationPlanStatus)}
                    >
                      <option value="draft">Entwurf</option>
                      <option value="active">Aktiv</option>
                    </select>
                  </label>

                  <div className="action-row">
                    <button
                      type="button"
                      className="btn btn-primary"
                      onClick={() => void handleCreatePlan()}
                      disabled={isCreatingPlan}
                    >
                      {isCreatingPlan ? "Lege an..." : "Durchlaufplan anlegen"}
                    </button>
                  </div>
                </div>
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <h2>Bestehende Durchlaufpläne</h2>
                <p>Alle bisherigen Pläne dieser Person inklusive historischer Stände.</p>
              </div>

              {rotationPlansQuery.isLoading ? (
                <LoadingState title="Durchlaufpläne werden geladen..." />
              ) : null}

              {!rotationPlansQuery.isLoading && rotationPlansQuery.error ? (
                <EmptyState
                  title="Durchlaufpläne konnten nicht geladen werden."
                  description={
                    rotationPlansQuery.error instanceof Error
                      ? rotationPlansQuery.error.message
                      : "Die Planliste ist fehlgeschlagen."
                  }
                  actionLabel="Erneut versuchen"
                  onAction={() => void rotationPlansQuery.refetch()}
                />
              ) : null}

              {!rotationPlansQuery.isLoading && !rotationPlansQuery.error && existingPlans.length === 0 ? (
                <p className="panel-note">Für diese Person gibt es noch keinen Durchlaufplan.</p>
              ) : null}

              {!rotationPlansQuery.isLoading && !rotationPlansQuery.error && existingPlans.length > 0 ? (
                <div className="workflow-grid" aria-label="Durchlaufpläne">
                  {existingPlans.map((plan) => (
                    <article key={plan.id} className="workflow-card card-list">
                      <div className="workflow-card-top">
                        <h3>{plan.title}</h3>
                        <span className={`status-pill ${getRotationPlanStatusPillClass(plan.status)}`}>
                          {getRotationPlanStatusLabel(plan.status)}
                        </span>
                      </div>

                      <dl className="workflow-meta">
                        <div>
                          <dt>Stationen</dt>
                          <dd>{plan.stationCount}</dd>
                        </div>
                        <div>
                          <dt>Zuletzt geändert</dt>
                          <dd>{formatDateTime(plan.updatedAt)}</dd>
                        </div>
                        <div>
                          <dt>Quell-Onboarding</dt>
                          <dd>{plan.sourceWorkflowUid}</dd>
                        </div>
                      </dl>

                      <div className="action-row">
                        <Link className="btn btn-primary" to={`/rotation/plans/${plan.id}`}>
                          Plan öffnen
                        </Link>
                      </div>
                    </article>
                  ))}
                </div>
              ) : null}
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}
