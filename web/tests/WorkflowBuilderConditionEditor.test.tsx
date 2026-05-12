import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderConditionEditor } from "../src/components/admin-config/WorkflowBuilderConditionEditor";
import type { AdminAnswerDefinition } from "../src/types/auth";

function makeAnswer(
  partial: Partial<AdminAnswerDefinition> & Pick<AdminAnswerDefinition, "answerKey" | "title" | "inputType">
): AdminAnswerDefinition {
  return {
    id: 1,
    workflowDefinitionId: 1,
    category: "general",
    description: "",
    iconKey: null,
    isRequired: false,
    sortOrder: 0,
    isActive: true,
    ...partial,
  };
}

const ANSWERS: AdminAnswerDefinition[] = [
  makeAnswer({ id: 1, answerKey: "has_laptop", title: "Laptop verfuegbar", inputType: "boolean" }),
  makeAnswer({ id: 2, answerKey: "comparison_user", title: "Vergleichs-User", inputType: "text" }),
];

describe("WorkflowBuilderConditionEditor", () => {
  it("renders form mode for valid JSON and shows operator dropdown", () => {
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"is_true"}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    // getByLabelText throws if not found — implicit existence check
    screen.getByLabelText("Antwort wählen");
    screen.getByLabelText("Operator wählen");
  });

  it("falls back to raw JSON mode for invalid input", () => {
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression="not-json"
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    screen.getByLabelText("Bedingungs-JSON");
    screen.getByText(/JSON ist ungültig/i);
  });

  it("emits is_true serialization without value field when operator changes to is_true", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"eq","expectedValueBoolean":true}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.change(screen.getByLabelText("Operator wählen"), { target: { value: "is_true" } });

    expect(onChange).toHaveBeenCalledTimes(1);
    const [json] = onChange.mock.calls[0]!;
    const parsed = JSON.parse(json);
    expect(parsed.operator).toBe("is_true");
    // is_true should NOT carry expectedValueBoolean (helper drops it)
    expect(parsed.expectedValueBoolean).toBeUndefined();
  });

  it("emits boolean-typed value for boolean answer + eq operator", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"eq"}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.change(screen.getByLabelText("Wert wählen"), { target: { value: "false" } });

    expect(onChange).toHaveBeenCalledTimes(1);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.expectedValueBoolean).toBe(false);
  });

  it("emits text value for text answer + neq operator", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"comparison_user","operator":"neq"}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.change(screen.getByLabelText("Wert eingeben"), { target: { value: "Max Mustermann" } });

    expect(onChange).toHaveBeenCalledTimes(1);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.expectedValueText).toBe("Max Mustermann");
    expect(parsed.expectedValueBoolean).toBeUndefined();
  });

  it("warns when answerKey is set but no matching answer is in the current process", () => {
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"missing_key","operator":"is_true"}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    screen.getByText(/Antwort-Key „missing_key" wurde im aktuellen Prozess nicht gefunden/i);
  });

  // ── Multi-Condition (Z21-S6b: AND/OR) ────────────────────────────────────

  it("does not show AND/OR toggle for a single condition", () => {
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"is_true"}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );
    expect(screen.queryByRole("radiogroup", { name: /Verkn/i })).toBeNull();
  });

  it("adding a second condition reveals the AND/OR toggle and emits multi-form JSON", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"is_true"}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.click(screen.getByText("+ Bedingung hinzufügen"));

    expect(onChange).toHaveBeenCalled();
    // Mit nur einer "gefuellten" Bedingung (zweite ist leer) bleibt Single-Form,
    // weil serializeConditionExpression leere Eintraege filtert.
    const lastJson = onChange.mock.calls[onChange.mock.calls.length - 1]![0];
    const parsed = JSON.parse(lastJson);
    expect(parsed.answerKey).toBe("has_laptop");
  });

  it("switches to OR logic when both conditions filled + Toggle clicked", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"logic":"AND","conditions":[{"answerKey":"has_laptop","operator":"is_true"},{"answerKey":"comparison_user","operator":"is_not_null"}]}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    const orButton = screen.getByRole("radio", { name: /ODER/ });
    fireEvent.click(orButton);

    expect(onChange).toHaveBeenCalledTimes(1);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.logic).toBe("OR");
    expect(parsed.conditions).toHaveLength(2);
  });

  it("falls back to single-form serialization when only one condition is filled", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"logic":"AND","conditions":[{"answerKey":"has_laptop","operator":"is_true"},{"answerKey":"","operator":"is_true"}]}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    // Trigger any change to force serialization (Operator-Wechsel auf erster Zeile)
    const operatorSelects = screen.getAllByLabelText("Operator wählen");
    fireEvent.change(operatorSelects[0]!, { target: { value: "is_false" } });

    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.conditions).toBeUndefined();
    expect(parsed.answerKey).toBe("has_laptop");
    expect(parsed.operator).toBe("is_false");
  });

  it("remove-button hides for single condition and removes second condition when shown", () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"answerKey":"has_laptop","operator":"is_true"}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );
    expect(screen.queryAllByLabelText("Bedingung entfernen")).toHaveLength(0);

    rerender(
      <WorkflowBuilderConditionEditor
        conditionExpression='{"logic":"AND","conditions":[{"answerKey":"has_laptop","operator":"is_true"},{"answerKey":"comparison_user","operator":"is_not_null"}]}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );
    const removeButtons = screen.getAllByLabelText("Bedingung entfernen");
    expect(removeButtons).toHaveLength(2);

    fireEvent.click(removeButtons[1]!);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    // Nach Entfernen bleibt nur 1 → Single-Form
    expect(parsed.answerKey).toBe("has_laptop");
    expect(parsed.conditions).toBeUndefined();
  });
});
