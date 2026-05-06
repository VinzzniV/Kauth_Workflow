import { useMemo } from "react";
import type {
  RequirementSelectionState,
  WorkflowRequirementSnapshot,
} from "../../types/workflow";
import { getRequirementEditorVisibleRequirements } from "../../utils/requirementEditor";
import {
  getRequirementEditorSelection,
  getRequirementSelection,
  hasRequirementSelectionChanges,
  type RequirementEntry,
} from "../../utils/requirements";
import { coerceIconKey } from "../../utils/iconRegistry";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import RequirementIcon from "./RequirementIcon";

type Props = {
  requirements: RequirementEntry[];
  mode?: "edit" | "view";
  selections?: Record<number, RequirementSelectionState>;
  onToggleBoolean?: (requirementId: number, value: boolean | null) => void;
  onTextChange?: (requirementId: number, value: string) => void;
  onSelectOption?: (requirementId: number, optionId: number | null) => void;
  onToggleMultiOption?: (requirementId: number, optionId: number) => void;
  isLoading: boolean;
  error: string | null;
  onRetry?: () => void;
  title?: string;
  description?: string;
};

type RequirementGroup = {
  categoryKey: string;
  categoryLabel: string;
  items: RequirementEntry[];
};

function toCategoryLabel(categoryKey: string): string {
  const normalized = categoryKey
    .replace(/[_-]+/g, " ")
    .trim()
    .toLowerCase();

  if (!normalized) {
    return "Allgemein";
  }

  return normalized
    .split(" ")
    .filter(Boolean)
    .map((token) => token.charAt(0).toUpperCase() + token.slice(1))
    .join(" ");
}

function sortRequirements(requirements: RequirementEntry[]): RequirementEntry[] {
  return [...requirements].sort((left, right) => left.sortOrder - right.sortOrder || left.id - right.id);
}

function getRequirementId(requirement: RequirementEntry): number {
  return requirement.id;
}

function isWorkflowRequirementSnapshotEntry(
  requirement: RequirementEntry
): requirement is WorkflowRequirementSnapshot {
  return "value" in requirement && "isVisible" in requirement;
}

function getIconKey(requirement: RequirementEntry): string {
  return "iconKey" in requirement ? coerceIconKey(requirement.iconKey) : coerceIconKey();
}

function findOptionLabel(requirement: RequirementEntry, optionId: number | null): string {
  if (optionId === null) {
    return "-";
  }

  return requirement.options.find((option) => option.id === optionId)?.label ?? "-";
}

function formatSelectionValue(requirement: RequirementEntry, selection: RequirementSelectionState): string {
  if (requirement.inputType === "boolean") {
    if (selection.valueBoolean === true) {
      return "Ja";
    }

    if (selection.valueBoolean === false) {
      return "Nein";
    }

    return "-";
  }

  if (requirement.inputType === "text") {
    return selection.valueText.trim() || "-";
  }

  if (requirement.inputType === "select") {
    return findOptionLabel(requirement, selection.selectedOptionId);
  }

  const labels = selection.selectedOptionIds
    .map((optionId) => findOptionLabel(requirement, optionId))
    .filter((label) => label !== "-");

  return labels.join(", ") || "-";
}

function renderReadOnlyRequirementField(
  requirement: RequirementEntry,
  selection: RequirementSelectionState
) {
  if (requirement.inputType === "text") {
    const textValue = selection.valueText.trim();
    return (
      <div className="field">
        <span>Antwort</span>
        <div className="requirement-readonly-value">
          {textValue || "Keine Angabe"}
        </div>
      </div>
    );
  }

  if (requirement.inputType === "select") {
    const label = findOptionLabel(requirement, selection.selectedOptionId);
    return (
      <div className="field">
        <span>Auswahl</span>
        <div className="chips-row" aria-label={`Auswahl für ${requirement.title}`}>
          <span className={`chip${label !== "-" ? " chip-action active" : ""}`}>
            {label !== "-" ? label : "Nicht ausgewählt"}
          </span>
        </div>
      </div>
    );
  }

  if (requirement.inputType === "multi_select") {
    const selectedLabels = selection.selectedOptionIds
      .map((optionId) => findOptionLabel(requirement, optionId))
      .filter((label) => label !== "-");

    return (
      <div className="field">
        <span>Auswahl</span>
        <div className="chips-row" aria-label={`Auswahl für ${requirement.title}`}>
          {selectedLabels.length > 0 ? (
            selectedLabels.map((label) => (
              <span key={label} className="chip chip-action active">
                {label}
              </span>
            ))
          ) : (
            <span className="chip">Nicht ausgewählt</span>
          )}
        </div>
      </div>
    );
  }

  return null;
}

function renderEditableRequirementField(
  requirement: RequirementEntry,
  selection: RequirementSelectionState,
  requirementId: number,
  onTextChange?: (requirementId: number, value: string) => void,
  onSelectOption?: (requirementId: number, optionId: number | null) => void,
  onToggleMultiOption?: (requirementId: number, optionId: number) => void
) {
  if (requirement.inputType === "text") {
    return (
      <label className="field">
        <span>Antwort</span>
        <input
          type="text"
          value={selection.valueText}
          onChange={(event) => onTextChange?.(requirementId, event.target.value)}
          placeholder="Bitte angeben"
        />
      </label>
    );
  }

  if (requirement.inputType === "select") {
    return (
      <label className="field">
        <span>Bitte auswählen</span>
        <select
          value={selection.selectedOptionId ?? ""}
          onChange={(event) =>
            onSelectOption?.(requirementId, event.target.value ? Number(event.target.value) : null)
          }
        >
          <option value="">Bitte auswählen</option>
          {requirement.options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </label>
    );
  }

  if (requirement.inputType === "multi_select") {
    return (
      <div className="field">
        <span>Auswahl</span>
        <div className="chips-row" role="group" aria-label={`Optionen für ${requirement.title}`}>
          {requirement.options.map((option) => {
            const isActive = selection.selectedOptionIds.includes(option.id);
            return (
              <button
                key={option.id}
                type="button"
                className={`chip chip-action ${isActive ? "active" : ""}`}
                aria-pressed={isActive}
                onClick={() => onToggleMultiOption?.(requirementId, option.id)}
              >
                {option.label}
              </button>
            );
          })}
        </div>
      </div>
    );
  }

  return null;
}

export default function RequirementsSelection({
  requirements,
  mode = "edit",
  selections,
  onToggleBoolean,
  onTextChange,
  onSelectOption,
  onToggleMultiOption,
  isLoading,
  error,
  onRetry,
  title = "Bedarf festlegen",
  description = "Wählen Sie aus, welche Zugänge und welche Ausstattung für den Vorgang benötigt werden.",
}: Props) {
  const effectiveSelections = selections;
  const visibleRequirements = useMemo(() => {
    const workflowRequirements = requirements.every(isWorkflowRequirementSnapshotEntry)
      ? requirements
      : null;

    if (workflowRequirements && !hasRequirementSelectionChanges(workflowRequirements, effectiveSelections)) {
      return workflowRequirements.filter((requirement) => requirement.isVisible);
    }

    return getRequirementEditorVisibleRequirements(requirements, effectiveSelections);
  }, [effectiveSelections, requirements]);

  const groupedRequirements = useMemo<RequirementGroup[]>(() => {
    const groups = new Map<string, RequirementEntry[]>();

    for (const requirement of sortRequirements(visibleRequirements)) {
      const categoryKey = requirement.category?.trim().toLowerCase() || "allgemein";
      const existing = groups.get(categoryKey) ?? [];
      existing.push(requirement);
      groups.set(categoryKey, existing);
    }

    return Array.from(groups.entries()).map(([categoryKey, items]) => ({
      categoryKey,
      categoryLabel: toCategoryLabel(categoryKey),
      items,
    }));
  }, [visibleRequirements]);

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>{title}</h2>
        <p>{description}</p>
      </div>

      {isLoading ? <LoadingState title="Auswahlpunkte werden geladen..." /> : null}

      {!isLoading && error ? (
        <EmptyState
          title="Auswahlpunkte konnten nicht geladen werden."
          description={error}
          actionLabel={onRetry ? "Erneut laden" : undefined}
          onAction={onRetry}
        />
      ) : null}

      {!isLoading && !error && requirements.length === 0 ? (
        <EmptyState
          title="Keine Auswahlpunkte hinterlegt"
          description="Derzeit sind keine Auswahlpunkte für diesen Vorgang hinterlegt."
        />
      ) : null}

      {!isLoading && !error && requirements.length > 0 ? (
        <div className="requirement-groups" aria-label="Auswahlgruppen">
          {groupedRequirements.map((group) => {
            const booleanRequirements = group.items.filter((requirement) => requirement.inputType === "boolean");
            const detailRequirements = group.items.filter((requirement) => requirement.inputType !== "boolean");

            return (
              <section key={group.categoryKey} className="requirement-group">
                <header className="requirement-group-head">
                  <h3>{group.categoryLabel}</h3>
                  <p>
                    {group.items.length} Punkt{group.items.length === 1 ? "" : "e"}
                  </p>
                </header>

                {mode === "edit" ? (
                  <>
                    {booleanRequirements.length > 0 ? (
                      <div className="requirement-toggle-grid" aria-label={`Schnellauswahl in ${group.categoryLabel}`}>
                        {booleanRequirements.map((requirement) => {
                          const selection = getRequirementEditorSelection(requirement, effectiveSelections);
                          const requirementId = getRequirementId(requirement);
                          const isActive = selection.valueBoolean === true;

                          return (
                            <button
                              key={requirementId}
                              type="button"
                              className={`requirement-toggle-card ${isActive ? "is-active" : ""}`}
                              aria-pressed={isActive}
                              onClick={() => onToggleBoolean?.(requirementId, isActive ? false : true)}
                            >
                              <div className="requirement-toggle-card__icon">
                                <RequirementIcon
                                  iconKey={getIconKey(requirement)}
                                  title={requirement.title}
                                  size="md"
                                />
                              </div>
                              <div className="requirement-toggle-card__body">
                                <span className="requirement-toggle-card__title">{requirement.title}</span>
                                <span className="requirement-toggle-card__description">{requirement.description}</span>
                              </div>
                              <span className="requirement-toggle-card__state">{isActive ? "Ja" : "Nein"}</span>
                            </button>
                          );
                        })}
                      </div>
                    ) : null}

                    {detailRequirements.length > 0 ? (
                      <ul className="requirements-list requirement-details-list" aria-label={`Detailfelder in ${group.categoryLabel}`}>
                        {detailRequirements.map((requirement) => {
                          const selection = getRequirementEditorSelection(requirement, effectiveSelections);
                          const requirementId = getRequirementId(requirement);

                          return (
                            <li key={requirementId} className="requirement-item requirement-card">
                              <div className="requirement-layout">
                                <div className="requirement-content">
                                  <div className="panel-head">
                                    <div className="requirement-title-row">
                                      <h4>{requirement.title}</h4>
                                    </div>
                                    <p>{requirement.description}</p>
                                  </div>

                                  {renderEditableRequirementField(
                                    requirement,
                                    selection,
                                    requirementId,
                                    onTextChange,
                                    onSelectOption,
                                    onToggleMultiOption
                                  )}
                                </div>

                                <div className="requirement-icon-side">
                                  <RequirementIcon iconKey={getIconKey(requirement)} title={requirement.title} />
                                </div>
                              </div>
                            </li>
                          );
                        })}
                      </ul>
                    ) : null}
                  </>
                ) : (
                  <>
                    {booleanRequirements.length > 0 ? (
                      <div className="requirement-toggle-grid" aria-label={`Auswahl in ${group.categoryLabel}`}>
                        {booleanRequirements.map((requirement) => {
                          const selection = getRequirementSelection(requirement, effectiveSelections);
                          const requirementId = getRequirementId(requirement);
                          const isActive = selection.valueBoolean === true;
                          const stateLabel = selection.valueBoolean === null
                            ? "Nicht ausgewählt"
                            : isActive
                              ? "Ja"
                              : "Nein";

                          return (
                            <div
                              key={requirementId}
                              className={`requirement-toggle-card requirement-toggle-card--readonly ${isActive ? "is-active" : ""}`}
                              aria-label={`${requirement.title}: ${stateLabel}`}
                            >
                              <div className="requirement-toggle-card__icon">
                                <RequirementIcon
                                  iconKey={getIconKey(requirement)}
                                  title={requirement.title}
                                  size="md"
                                />
                              </div>
                              <div className="requirement-toggle-card__body">
                                <span className="requirement-toggle-card__title">{requirement.title}</span>
                                <span className="requirement-toggle-card__description">{requirement.description}</span>
                              </div>
                              <span className="requirement-toggle-card__state">{stateLabel}</span>
                            </div>
                          );
                        })}
                      </div>
                    ) : null}

                    {detailRequirements.length > 0 ? (
                      <ul className="requirements-list requirement-details-list" aria-label={`Detailfelder in ${group.categoryLabel}`}>
                        {detailRequirements.map((requirement) => {
                          const selection = getRequirementSelection(requirement, effectiveSelections);
                          const requirementId = getRequirementId(requirement);

                          return (
                            <li key={requirementId} className="requirement-item requirement-card">
                              <div className="requirement-layout">
                                <div className="requirement-content">
                                  <div className="panel-head">
                                    <div className="requirement-title-row">
                                      <h4>{requirement.title}</h4>
                                    </div>
                                    <p>{requirement.description}</p>
                                  </div>

                                  {renderReadOnlyRequirementField(requirement, selection) ?? (
                                    <p className="panel-note">{formatSelectionValue(requirement, selection)}</p>
                                  )}
                                </div>

                                <div className="requirement-icon-side">
                                  <RequirementIcon iconKey={getIconKey(requirement)} title={requirement.title} />
                                </div>
                              </div>
                            </li>
                          );
                        })}
                      </ul>
                    ) : null}
                  </>
                )}
              </section>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}
