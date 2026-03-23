import { useMemo } from "react";
import type {
  RequirementSelectionState,
} from "../../types/workflow";
import {
  getRequirementSelection,
  getVisibleRequirements,
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
  description = "Wählen Sie aus, welche Zugänge und welche Ausstattung für die neue Person benötigt werden.",
}: Props) {
  const effectiveSelections = mode === "edit" ? selections : undefined;
  const visibleRequirements = useMemo(() => {
    if (mode === "view") {
      return requirements.filter((requirement) => ("isVisible" in requirement ? requirement.isVisible : true));
    }

    return getVisibleRequirements(requirements, effectiveSelections);
  }, [effectiveSelections, mode, requirements]);

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
          description="Derzeit sind keine Auswahlpunkte für das Onboarding hinterlegt."
        />
      ) : null}

      {!isLoading && !error && requirements.length > 0 ? (
        <div className="requirement-groups" aria-label="Auswahlgruppen">
          {groupedRequirements.map((group) => (
            <section key={group.categoryKey} className="requirement-group">
              <header className="requirement-group-head">
                <h3>{group.categoryLabel}</h3>
                <p>
                  {group.items.length} Punkt{group.items.length === 1 ? "" : "e"}
                </p>
              </header>

              <ul className="requirements-list" aria-label={`Auswahlpunkte in ${group.categoryLabel}`}>
                {group.items.map((requirement) => {
                  const selection = getRequirementSelection(requirement, effectiveSelections);
                  const requirementId = getRequirementId(requirement);
                  const isViewMode = mode === "view";
                  const selectionValue = formatSelectionValue(requirement, selection);

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

                          {isViewMode ? (
                            <p className="panel-note">Gespeicherter Wert: {selectionValue}</p>
                          ) : (() => {
                            if (requirement.inputType === "boolean") {
                              return (
                                <div className="toggle-group" role="group" aria-label={`Antwort für ${requirement.title}`}>
                                  <button
                                    type="button"
                                    className={`toggle-btn ${selection.valueBoolean === true ? "active" : ""}`}
                                    onClick={() => onToggleBoolean?.(requirementId, true)}
                                  >
                                    Ja
                                  </button>
                                  <button
                                    type="button"
                                    className={`toggle-btn ${selection.valueBoolean === false ? "active" : ""}`}
                                    onClick={() => onToggleBoolean?.(requirementId, false)}
                                  >
                                    Nein
                                  </button>
                                </div>
                              );
                            }

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
                                <div className="chips-row" aria-label={`Mehrfachauswahl für ${requirement.title}`}>
                                  {requirement.options.map((option) => {
                                    const isActive = selection.selectedOptionIds.includes(option.id);
                                    return (
                                      <button
                                        type="button"
                                        key={option.id}
                                        className={`chip chip-action ${isActive ? "active" : ""}`}
                                        onClick={() => onToggleMultiOption?.(requirementId, option.id)}
                                      >
                                        {option.label}
                                      </button>
                                    );
                                  })}
                                </div>
                              );
                            }

                            return null;
                          })()}
                        </div>

                        <div className="requirement-icon-side">
                          <RequirementIcon iconKey={getIconKey(requirement)} title={requirement.title} />
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </section>
          ))}
        </div>
      ) : null}
    </section>
  );
}
