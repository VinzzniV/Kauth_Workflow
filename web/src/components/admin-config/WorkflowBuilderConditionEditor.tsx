import { useState } from "react";
import { X } from "lucide-react";
import type { AdminAnswerDefinition } from "../../types/auth";
import {
  ALL_CONDITION_OPERATORS,
  parseConditionExpression,
  serializeConditionExpression,
  type ConditionOperator,
  type DecisionConditionLogic,
  type ParsedCondition,
  type ParsedDecisionExpression,
} from "./workflowBuilderEditorHelpers";

const OPERATOR_LABELS: Record<ConditionOperator, string> = {
  is_true: "ist Ja (true)",
  is_false: "ist Nein (false)",
  is_not_null: "ist gepflegt",
  is_null: "ist leer",
  eq: "ist gleich",
  neq: "ist ungleich",
};

const BOOL_OPERATORS: ConditionOperator[] = ALL_CONDITION_OPERATORS;
const TEXT_OPERATORS: ConditionOperator[] = ["is_not_null", "is_null", "eq", "neq"];

type WorkflowBuilderConditionEditorProps = {
  conditionExpression: string;
  answerDefinitions: AdminAnswerDefinition[];
  onChange: (next: string) => void;
};

function emptyCondition(): ParsedCondition {
  return {
    answerKey: "",
    operator: "is_true",
    expectedValueText: "",
    expectedValueBoolean: null,
    expectedValueNumber: "",
  };
}

export function WorkflowBuilderConditionEditor({
  conditionExpression,
  answerDefinitions,
  onChange,
}: WorkflowBuilderConditionEditorProps) {
  const initial = parseConditionExpression(conditionExpression);
  const [rawMode, setRawMode] = useState(initial === "invalid");

  if (rawMode || initial === "invalid") {
    return (
      <div className="wf-condition-editor wf-condition-editor--raw">
        <textarea
          className="form-textarea wf-step-card-configtext"
          rows={4}
          value={conditionExpression}
          onChange={(e) => onChange(e.target.value)}
          placeholder='{ "answerKey": "...", "operator": "is_true" } oder { "logic": "AND", "conditions": [ ... ] }'
          spellCheck={false}
          aria-label="Bedingungs-JSON"
        />
        <button
          type="button"
          className="wf-condition-mode-toggle"
          onClick={() => setRawMode(false)}
          disabled={parseConditionExpression(conditionExpression) === "invalid"}
        >
          ← Zurück zum Form-Editor
        </button>
        {parseConditionExpression(conditionExpression) === "invalid" && conditionExpression.trim() && (
          <p className="wf-form-field-hint text-error">
            Aktuelles JSON ist ungültig — bitte korrigieren oder leer machen.
          </p>
        )}
      </div>
    );
  }

  const expr = initial;
  const sortedAnswers = [...answerDefinitions].sort((a, b) =>
    (a.title ?? "").localeCompare(b.title ?? "", "de")
  );

  const emit = (nextExpr: ParsedDecisionExpression) => {
    onChange(serializeConditionExpression(nextExpr));
  };

  const updateCondition = (index: number, next: Partial<ParsedCondition>) => {
    const conditions = expr.conditions.map((c, i) => (i === index ? { ...c, ...next } : c));
    emit({ ...expr, conditions });
  };

  const addCondition = () => {
    emit({ ...expr, conditions: [...expr.conditions, emptyCondition()] });
  };

  const removeCondition = (index: number) => {
    if (expr.conditions.length <= 1) return;
    emit({ ...expr, conditions: expr.conditions.filter((_, i) => i !== index) });
  };

  const setLogic = (logic: DecisionConditionLogic) => {
    emit({ ...expr, logic });
  };

  const isMulti = expr.conditions.length >= 2;

  return (
    <div className="wf-condition-editor">
      {isMulti && (
        <div className="wf-condition-logic-toggle" role="radiogroup" aria-label="Verknüpfung der Bedingungen">
          <button
            type="button"
            className={`wf-condition-logic-option${expr.logic === "AND" ? " is-active" : ""}`}
            role="radio"
            aria-checked={expr.logic === "AND"}
            onClick={() => setLogic("AND")}
          >
            UND (alle müssen zutreffen)
          </button>
          <button
            type="button"
            className={`wf-condition-logic-option${expr.logic === "OR" ? " is-active" : ""}`}
            role="radio"
            aria-checked={expr.logic === "OR"}
            onClick={() => setLogic("OR")}
          >
            ODER (mindestens eine)
          </button>
        </div>
      )}

      <div className="wf-condition-list">
        {expr.conditions.map((c, index) => (
          <ConditionRow
            key={index}
            parsed={c}
            answers={sortedAnswers}
            canRemove={isMulti}
            onChange={(next) => updateCondition(index, next)}
            onRemove={() => removeCondition(index)}
          />
        ))}
      </div>

      <div className="wf-condition-actions">
        <button type="button" className="btn btn-secondary btn-sm" onClick={addCondition}>
          + Bedingung hinzufügen
        </button>
        <button
          type="button"
          className="wf-condition-mode-toggle"
          onClick={() => setRawMode(true)}
        >
          JSON-Editor (Power) →
        </button>
      </div>

      <p className="wf-form-field-hint">
        Beispiel: „Antwort 'Standort' = 'München' UND Antwort 'Laptop' ist Ja" — beide müssen zutreffen, damit
        dieser Pfad greift.
      </p>
    </div>
  );
}

function ConditionRow({
  parsed,
  answers,
  canRemove,
  onChange,
  onRemove,
}: {
  parsed: ParsedCondition;
  answers: AdminAnswerDefinition[];
  canRemove: boolean;
  onChange: (next: Partial<ParsedCondition>) => void;
  onRemove: () => void;
}) {
  const selectedAnswer = answers.find((a) => a.answerKey === parsed.answerKey) ?? null;
  const inputType = selectedAnswer?.inputType ?? "text";
  const availableOperators =
    inputType === "boolean" ? BOOL_OPERATORS
    : inputType === "text" ? TEXT_OPERATORS
    : ALL_CONDITION_OPERATORS;
  const operatorIsSupported = availableOperators.includes(parsed.operator);
  const showValueField = parsed.operator === "eq" || parsed.operator === "neq";

  return (
    <div className="wf-condition-row">
      <div className="wf-condition-fields">
        <select
          className="form-select"
          value={parsed.answerKey}
          onChange={(e) => onChange({ answerKey: e.target.value })}
          aria-label="Antwort wählen"
        >
          <option value="">– Antwort wählen –</option>
          {answers.map((ans) => (
            <option key={ans.id} value={ans.answerKey}>
              {ans.title} ({ans.answerKey})
            </option>
          ))}
        </select>

        <select
          className="form-select"
          value={parsed.operator}
          onChange={(e) => onChange({ operator: e.target.value as ConditionOperator })}
          aria-label="Operator wählen"
        >
          {ALL_CONDITION_OPERATORS.map((op) => (
            <option key={op} value={op} disabled={!availableOperators.includes(op)}>
              {OPERATOR_LABELS[op]}
            </option>
          ))}
        </select>

        {showValueField && inputType === "boolean" && (
          <select
            className="form-select"
            value={parsed.expectedValueBoolean === null ? "" : String(parsed.expectedValueBoolean)}
            onChange={(e) =>
              onChange({
                expectedValueBoolean: e.target.value === "" ? null : e.target.value === "true",
                expectedValueText: "",
                expectedValueNumber: "",
              })
            }
            aria-label="Wert wählen"
          >
            <option value="">– Wert –</option>
            <option value="true">Ja</option>
            <option value="false">Nein</option>
          </select>
        )}

        {showValueField && inputType !== "boolean" && (
          <input
            className="form-input"
            type="text"
            value={parsed.expectedValueText}
            onChange={(e) =>
              onChange({
                expectedValueText: e.target.value,
                expectedValueBoolean: null,
                expectedValueNumber: "",
              })
            }
            placeholder="Vergleichswert"
            aria-label="Wert eingeben"
          />
        )}

        {canRemove && (
          <button
            type="button"
            className="wf-step-card-iconbtn wf-step-card-iconbtn--danger"
            onClick={onRemove}
            title="Bedingung entfernen"
            aria-label="Bedingung entfernen"
          >
            <X size={16} aria-hidden="true" />
          </button>
        )}
      </div>

      {selectedAnswer && !operatorIsSupported && (
        <p className="wf-form-field-hint text-error">
          Operator passt nicht zum Antwort-Typ „{inputType}". Bitte einen anderen Operator wählen.
        </p>
      )}

      {!selectedAnswer && parsed.answerKey && (
        <p className="wf-form-field-hint text-error">
          Antwort-Key „{parsed.answerKey}" wurde im aktuellen Prozess nicht gefunden.
        </p>
      )}
    </div>
  );
}
