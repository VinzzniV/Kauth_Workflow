import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import { useToast } from "../components/feedback/useToast";
import PageHeader from "../components/layout/PageHeader";
import { createRotationPlan } from "../services/rotationApi";
import { queryKeys } from "../services/queryKeys";
import { useRotationEligiblePeople, useRotationPlans } from "../services/queries/rotationQueries";
import type {
  RotationEligiblePerson,
  RotationPlanStatus,
} from "../types/rotation";
import { formatDateTime } from "../utils/dateFormat";
import { formatEmploymentStatus } from "../utils/employmentStatus";

function FilterChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="filter-chip">
      <strong>{label}</strong>
      <span>{value}</span>
    </span>
  );
}

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

function formatUid(uid: string | null | undefined): string {
  if (!uid) return "-";
  return uid.length > 8 ? `${uid.slice(0, 8)}…` : uid;
}


export default function RotationPlanningPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();
  const isCreateMode = searchParams.get("mode") === "create";
  const [search, setSearch] = useState("");
  const [planSearch, setPlanSearch] = useState("");
  const [selectedPerson, setSelectedPerson] = useState<RotationEligiblePerson | null>(null);
  const [planTitle, setPlanTitle] = useState("");
  const [planStatus, setPlanStatus] = useState<RotationPlanStatus>("draft");
  const [isCreatingPlan, setIsCreatingPlan] = useState(false);

  const eligiblePeopleQuery = useRotationEligiblePeople(search, isCreateMode);
  const rotationPlansQuery = useRotationPlans(
    isCreateMode ? selectedPerson?.personId ?? null : null,
    !isCreateMode || selectedPerson?.personId !== undefined
  );

  const eligiblePeople = eligiblePeopleQuery.data;
  const existingPlans = useMemo(() => rotationPlansQuery.data ?? [], [rotationPlansQuery.data]);
  const visiblePlans = useMemo(() => {
    const normalizedSearch = planSearch.trim().toLowerCase();
    if (!normalizedSearch) {
      return existingPlans;
    }

    return existingPlans.filter((plan) =>
      plan.title.toLowerCase().includes(normalizedSearch)
      || plan.displayName.toLowerCase().includes(normalizedSearch)
      || (plan.departmentName ?? "").toLowerCase().includes(normalizedSearch)
      || plan.sourceWorkflowUid.toLowerCase().includes(normalizedSearch)
    );
  }, [existingPlans, planSearch]);
  const selectedPersonName = selectedPerson?.displayName ?? "Person";
  const hasCreateSearch = search.trim().length > 0;
  const hasPlanSearch = planSearch.trim().length > 0;

  async function handleCreatePlan() {
    if (!selectedPerson) {
      showError("Bitte zuerst eine Person auswählen.");
      return;
    }

    if (!selectedPerson.latestSourceWorkflowUid) {
      showError("Für die gewählte Person fehlt ein Quell-Workflow als Referenz.");
      return;
    }

    setIsCreatingPlan(true);
    try {
      const createdPlan = await createRotationPlan({
        personId: selectedPerson.personId,
        sourceWorkflowUid: selectedPerson.latestSourceWorkflowUid,
        title: planTitle.trim() || undefined,
        status: planStatus,
      });

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(selectedPerson.personId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(null) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.people.rotationEligible(search) }),
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
          description={
            isCreateMode
              ? "Neuen Abteilungsdurchlauf auf Basis einer bestehenden Person mit abgeschlossenem Onboarding starten."
              : "Übersicht über bestehende Durchlaufpläne, Stände und Einstiege in die Detailansicht."
          }
        />

        {isCreateMode ? (
          <div>
            <Link to="/rotation" className="btn btn-secondary">
              ← Zurück zur Übersicht
            </Link>
          </div>
        ) : null}


        {isCreateMode ? (
          <section className="panel panel-muted">
            <div className="panel-head">
              <h2>Person mit abgeschlossenem Onboarding suchen</h2>
              <p>Die Suche liefert nur Personen, deren letztes Onboarding bereits abgeschlossen wurde.</p>
            </div>

            <div className="toolbar-row toolbar-row-filters">
              <label className="field compact grow">
                <span>Suche</span>
                <input
                  type="text"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Name, Personalnummer, Abteilung oder UPN"
                />
              </label>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => void eligiblePeopleQuery.refetch()}
                disabled={eligiblePeopleQuery.isFetching}
              >
                {eligiblePeopleQuery.isFetching ? "Aktualisiere..." : "Aktualisieren"}
              </button>
            </div>
            <div className="filter-panel-actions" aria-live="polite">
              <div className="filter-chip-row" aria-label="Aktive Filter">
                {hasCreateSearch ? <FilterChip label="Suche" value={search.trim()} /> : <span className="filter-chip-empty">Keine aktiven Filter.</span>}
              </div>
              <button type="button" className="btn btn-ghost" onClick={() => setSearch("")} disabled={!hasCreateSearch}>
                Filter zurücksetzen
              </button>
            </div>
          </section>
        ) : (
          <section className="panel panel-muted">
            <div className="panel-head">
              <h2>Neuen Durchlauf starten</h2>
              <p>
                Wählen Sie eine Person mit abgeschlossenem Onboarding aus, um einen neuen
                Abteilungsdurchlauf anzulegen.
              </p>
            </div>
            <div className="action-row">
              <Link className="btn btn-primary" to="/rotation?mode=create">
                Neuen Durchlaufplan anlegen
              </Link>
            </div>
          </section>
        )}

        {isCreateMode && eligiblePeopleQuery.isLoading ? (
          <LoadingState title="Personen mit abgeschlossenem Onboarding werden geladen..." />
        ) : null}

        {isCreateMode && !eligiblePeopleQuery.isLoading && eligiblePeopleQuery.error ? (
          <EmptyState
            title="Personen konnten nicht geladen werden."
            description={
              eligiblePeopleQuery.error instanceof Error
                ? eligiblePeopleQuery.error.message
                : "Die Suche ist fehlgeschlagen."
            }
            actionLabel="Erneut versuchen"
            onAction={() => void eligiblePeopleQuery.refetch()}
          />
        ) : null}

        {isCreateMode && !eligiblePeopleQuery.isLoading &&
        !eligiblePeopleQuery.error &&
        (eligiblePeople?.length ?? 0) > 0 ? (
          <section className="workflow-grid" aria-label="Rotationsberechtigte Personen">
            {(eligiblePeople ?? []).map((entry) => {
              const isSelected = selectedPerson?.personId === entry.personId;
              return (
                <article key={entry.personId} className="workflow-card card-list">
                  <div className="workflow-card-top">
                    <h2>{entry.displayName}</h2>
                    <span className={`status-pill ${isSelected ? "running" : "open"}`}>
                      {isSelected ? "Ausgewählt" : "Verfügbar"}
                    </span>
                  </div>
                  <dl className="workflow-meta">
                    <div>
                      <dt>Stamm-Abteilung</dt>
                      <dd>{entry.departmentName ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>Rolle</dt>
                      <dd>{entry.roleName ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>Personalnummer</dt>
                      <dd>{entry.employeeNumber ?? "-"}</dd>
                    </div>
                    <div>
                      <dt>Status</dt>
                      <dd>{formatEmploymentStatus(entry.employmentStatus)}</dd>
                    </div>
                    <div>
                      <dt>Letzter Quell-Workflow</dt>
                      <dd>{formatDateTime(entry.latestSourceWorkflowCompletedAt)}</dd>
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

        {isCreateMode && !eligiblePeopleQuery.isLoading &&
        !eligiblePeopleQuery.error &&
        (eligiblePeople?.length ?? 0) === 0 ? (
          <EmptyState
            title="Keine geeigneten Personen gefunden"
            description="Passen Sie die Suche an oder prüfen Sie, ob bereits Onboardings abgeschlossen wurden."
          />
        ) : null}

        {isCreateMode && selectedPerson ? (
          <>
            <section className="panel">
              <div className="panel-head">
                <h2>Personendetail</h2>
                <p>Der Durchlaufplan referenziert die Person. Das letzte abgeschlossene Onboarding bleibt nur der Provenienz-Nachweis.</p>
              </div>

              <dl className="workflow-meta">
                <div>
                  <dt>Name</dt>
                  <dd>{selectedPersonName}</dd>
                </div>
                <div>
                  <dt>Stamm-Abteilung</dt>
                  <dd>{selectedPerson.departmentName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Rolle</dt>
                  <dd>{selectedPerson.roleName ?? "-"}</dd>
                </div>
                <div>
                  <dt>Beschäftigungsstatus</dt>
                  <dd>{formatEmploymentStatus(selectedPerson.employmentStatus)}</dd>
                </div>
                <div>
                  <dt>Quell-Workflow</dt>
                  <dd title={selectedPerson.latestSourceWorkflowUid ?? undefined} className="uid-value">
                    {formatUid(selectedPerson.latestSourceWorkflowUid)}
                  </dd>
                </div>
                <div>
                  <dt>Abgeschlossen</dt>
                  <dd>{formatDateTime(selectedPerson.latestSourceWorkflowCompletedAt)}</dd>
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
                <div className="card-primary card-form">
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
                          <dd title={plan.sourceWorkflowUid} className="uid-value">
                            {formatUid(plan.sourceWorkflowUid)}
                          </dd>
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

        {!isCreateMode ? (
          <section className="panel">
            <div className="panel-head">
              <h2>Bestehende Durchlaufpläne</h2>
              <p>Aktive, abgeschlossene und archivierte Pläne aus den sichtbaren Abteilungen.</p>
            </div>

            <div className="toolbar-row toolbar-row-filters">
              <label className="field compact grow">
                <span>Suche</span>
                <input
                  type="text"
                  value={planSearch}
                  onChange={(event) => setPlanSearch(event.target.value)}
                  placeholder="Name, Titel, Abteilung oder Quell-Onboarding"
                />
              </label>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => void rotationPlansQuery.refetch()}
                disabled={rotationPlansQuery.isFetching}
              >
                {rotationPlansQuery.isFetching ? "Aktualisiere..." : "Aktualisieren"}
              </button>
            </div>
            <div className="filter-panel-actions" aria-live="polite">
              <div className="filter-chip-row" aria-label="Aktive Filter">
                {hasPlanSearch ? <FilterChip label="Suche" value={planSearch.trim()} /> : <span className="filter-chip-empty">Keine aktiven Filter.</span>}
              </div>
              <button type="button" className="btn btn-ghost" onClick={() => setPlanSearch("")} disabled={!hasPlanSearch}>
                Filter zurücksetzen
              </button>
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

            {!rotationPlansQuery.isLoading && !rotationPlansQuery.error && visiblePlans.length === 0 ? (
              <EmptyState
                title={
                  planSearch.trim().length > 0
                    ? "Keine passenden Durchlaufpläne"
                    : "Noch keine Durchlaufpläne"
                }
                description={
                  planSearch.trim().length > 0
                    ? "Passen Sie die Suche an oder löschen Sie den Suchbegriff, um alle sichtbaren Pläne zu sehen."
                    : "Wählen Sie eine Person mit abgeschlossenem Onboarding aus, um den ersten Abteilungsdurchlauf anzulegen."
                }
                actionLabel={planSearch.trim().length > 0 ? "Suche zurücksetzen" : "Neuen Durchlaufplan anlegen"}
                onAction={() => {
                  if (planSearch.trim().length > 0) {
                    setPlanSearch("");
                  } else {
                    void navigate("/rotation?mode=create");
                  }
                }}
              />
            ) : null}

            {!rotationPlansQuery.isLoading && !rotationPlansQuery.error && visiblePlans.length > 0 ? (
              <div className="workflow-grid" aria-label="Durchlaufpläne">
                {visiblePlans.map((plan) => (
                  <article key={plan.id} className="workflow-card card-list">
                    <div className="workflow-card-top">
                      <h3>{plan.title}</h3>
                      <span className={`status-pill ${getRotationPlanStatusPillClass(plan.status)}`}>
                        {getRotationPlanStatusLabel(plan.status)}
                      </span>
                    </div>

                    <dl className="workflow-meta">
                      <div>
                        <dt>Person</dt>
                        <dd>{plan.displayName}</dd>
                      </div>
                      <div>
                        <dt>Abteilung</dt>
                        <dd>{plan.departmentName ?? "-"}</dd>
                      </div>
                      <div>
                        <dt>Stationen</dt>
                        <dd>{plan.stationCount}</dd>
                      </div>
                      <div>
                        <dt>Zuletzt geändert</dt>
                        <dd>{formatDateTime(plan.updatedAt)}</dd>
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
        ) : null}
      </div>
    </main>
  );
}
