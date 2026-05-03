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
});
