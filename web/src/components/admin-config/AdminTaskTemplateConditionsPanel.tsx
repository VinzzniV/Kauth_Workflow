import type { AdminAnswerDefinition } from "../../types/auth";
import type { ConditionDraft, groupConditions } from "../../hooks/adminTaskTemplateManagementModel";
import SectionHeader from "../ui/SectionHeader";

type AdminTaskTemplateConditionsPanelProps = {
  groupedConditions: ReturnType<typeof groupConditions>;
  conditionDraft: ConditionDraft;
  answerDefinitions: AdminAnswerDefinition[];
  isLoadingConditions: boolean;
  isSavingCondition: boolean;
  deletingConditionId: number | null;
  onUpdateConditionDraft: <K extends keyof ConditionDraft>(key: K, value: ConditionDraft[K]) => void;
  onAddConditionGroup: () => void;
  onAddCondition: () => Promise<void>;
  onRemoveCondition: (conditionId: number) => Promise<void>;
};

export function AdminTaskTemplateConditionsPanel({
  groupedConditions,
  conditionDraft,
  answerDefinitions,
  isLoadingConditions,
  isSavingCondition,
  deletingConditionId,
  onUpdateConditionDraft,
  onAddConditionGroup,
  onAddCondition,
  onRemoveCondition,
}: AdminTaskTemplateConditionsPanelProps) {
  return (
    <section className="panel">
      <SectionHeader title="Bedingungen" />

      <div className="content-stack">
        {isLoadingConditions ? <p className="panel-note">Bedingungen werden geladen...</p> : null}

        {!isLoadingConditions && groupedConditions.length === 0 ? (
          <p className="panel-note">Für diese Aufgabenvorlage sind noch keine Bedingungen definiert.</p>
        ) : null}

        {!isLoadingConditions && groupedConditions.length > 0 ? (
          <div className="admin-condition-group-list">
            {groupedConditions.map((group) => (
              <article key={group.group} className="admin-condition-group">
                <div className="admin-condition-group-head">
                  <h3>Gruppe {group.group}</h3>
                  <p>Alle Regeln dieser Gruppe müssen gleichzeitig erfüllt sein.</p>
                </div>

                <table className="table">
                  <thead>
                    <tr>
                      <th>Answer Key</th>
                      <th>Operator</th>
                      <th>Text</th>
                      <th>Bool</th>
                      <th>Zahl</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {group.items.map((condition) => (
                      <tr key={condition.id}>
                        <td className="text-muted">{condition.answerKey}</td>
                        <td>{condition.operator}</td>
                        <td>{condition.expectedValueText ?? "—"}</td>
                        <td>
                          {condition.expectedValueBoolean === null
                            ? "—"
                            : condition.expectedValueBoolean
                              ? "true"
                              : "false"}
                        </td>
                        <td>{condition.expectedValueNumber ?? "—"}</td>
                        <td>
                          <button
                            type="button"
                            className="btn btn-sm btn-outline"
                            disabled={deletingConditionId === condition.id || isSavingCondition}
                            onClick={() => void onRemoveCondition(condition.id)}
                          >
                            {deletingConditionId === condition.id ? "Löscht..." : "Entfernen"}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </article>
            ))}
          </div>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          <label>
            <span className="form-label">Bedingungsgruppe</span>
            <input
              className="form-input"
              type="number"
              min="1"
              value={conditionDraft.conditionGroup}
              onChange={(event) => onUpdateConditionDraft("conditionGroup", event.target.value)}
            />
          </label>

          <label>
            <span className="form-label">Antwortfeld</span>
            <select className="form-select" value={conditionDraft.answerKey} onChange={(event) => onUpdateConditionDraft("answerKey", event.target.value)}>
              <option value="">Antwortfeld wählen</option>
              {answerDefinitions.map((definition) => (
                <option key={definition.id} value={definition.answerKey}>
                  {definition.answerKey} ({definition.title})
                </option>
              ))}
            </select>
          </label>

          <label>
            <span className="form-label">Operator</span>
            <select
              className="form-select"
              value={conditionDraft.operator}
              onChange={(event) =>
                onUpdateConditionDraft(
                  "operator",
                  event.target.value as "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null"
                )
              }
            >
              <option value="eq">eq</option>
              <option value="neq">neq</option>
              <option value="is_true">is_true</option>
              <option value="is_false">is_false</option>
              <option value="is_null">is_null</option>
              <option value="is_not_null">is_not_null</option>
            </select>
          </label>

          <label>
            <span className="form-label">Erwarteter Text</span>
            <input className="form-input" value={conditionDraft.expectedValueText} onChange={(event) => onUpdateConditionDraft("expectedValueText", event.target.value)} />
          </label>

          <label>
            <span className="form-label">Erwarteter Wahr/Falsch-Wert</span>
            <select className="form-select" value={conditionDraft.expectedValueBoolean} onChange={(event) => onUpdateConditionDraft("expectedValueBoolean", event.target.value as "true" | "false" | "")}>
              <option value="">leer</option>
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          </label>

          <label>
            <span className="form-label">Erwartete Zahl</span>
            <input className="form-input" type="number" value={conditionDraft.expectedValueNumber} onChange={(event) => onUpdateConditionDraft("expectedValueNumber", event.target.value)} />
          </label>
        </div>

        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <button type="button" className="btn btn-secondary" disabled={isSavingCondition} onClick={onAddConditionGroup}>
            Neue Gruppe vorbereiten
          </button>

          <button type="button" className="btn btn-primary" disabled={isSavingCondition} onClick={() => void onAddCondition()}>
            {isSavingCondition ? "Wird angelegt..." : "Bedingung hinzufügen"}
          </button>
        </div>
      </div>
    </section>
  );
}
