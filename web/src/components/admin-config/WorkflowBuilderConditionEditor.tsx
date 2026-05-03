import { useState } from "react";
import type { AdminAnswerDefinition } from "../../types/auth";
import {
  ALL_CONDITION_OPERATORS,
  parseCondition,
  serializeCondition,
  type ConditionOperator,
  type ParsedCondition,
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

export function WorkflowBuilderConditionEditor({
  conditionExpression,
  answerDefinitions,
  onChange,
}: WorkflowBuilderConditionEditorProps) {
  const initial = parseCondition(conditionExpression);
  const [rawMode, setRawMode] = useState(initial === "invalid");

  if (rawMode || initial === "invalid") {
    return (
      <div className="wf-condition-editor wf-condition-editor--raw">
        <textarea
          className="form-textarea wf-step-card-configtext"
          rows={3}
          value={conditionExpression}
          onChange={(e) => onChange(e.target.value)}
          placeholder='{ "answerKey": "...", "operator": "is_true" }'
          spellCheck={false}
          aria-label="Bedingungs-JSON"
        />
        <button
          type="button"
          className="wf-condition-mode-toggle"
          onClick={() => setRawMode(false)}
          disabled={parseCondition(conditionExpression) === "invalid"}
        >
          ← Zurück zum Form-Editor
        </button>
        {parseCondition(conditionExpression) === "invalid" && conditionExpression.trim() && (
          <p className="wf-form-field-hint text-error">
            Aktuelles JSON ist ungültig — bitte korrigieren oder leer machen.
          </p>
        )}
      </div>
    );
  }

  const parsed = initial;
  const sortedAnswers = [...answerDefinitions].sort((a, b) =>
    (a.title ?? "").localeCompare(b.title ?? "", "de")
  );
  const selectedAnswer = sortedAnswers.find((a) => a.answerKey === parsed.answerKey) ?? null;
  const inputType = selectedAnswer?.inputType ?? "text";
  const availableOperators =
    inputType === "boolean" ? BOOL_OPERATORS
    : inputType === "text" ? TEXT_OPERATORS
    : ALL_CONDITION_OPERATORS;
  const operatorIsSupported = availableOperators.includes(parsed.operator);

  const update = (next: Partial<ParsedCondition>) => {
    const merged = { ...parsed, ...next };
    onChange(serializeCondition(merged));
  };

  const showValueField = parsed.operator === "eq" || parsed.operator === "neq";

  return (
    <div className="wf-condition-editor">
      <div className="wf-condition-fields">
        <select
          className="form-select"
          value={parsed.answerKey}
          onChange={(e) => update({ answerKey: e.target.value })}
          aria-label="Antwort wählen"
        >
          <option value="">– Antwort wählen –</option>
          {sortedAnswers.map((ans) => (
            <option key={ans.id} value={ans.answerKey}>
              {ans.title} ({ans.answerKey})
            </option>
          ))}
        </select>

        <select
          className="form-select"
          value={parsed.operator}
          onChange={(e) => update({ operator: e.target.value as ConditionOperator })}
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
              update({
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
              update({
                expectedValueText: e.target.value,
                expectedValueBoolean: null,
                expectedValueNumber: "",
              })
            }
            placeholder="Vergleichswert"
            aria-label="Wert eingeben"
          />
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

      <button
        type="button"
        className="wf-condition-mode-toggle"
        onClick={() => setRawMode(true)}
      >
        JSON-Editor (Power) →
      </button>
    </div>
  );
}

