import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderActionEditor } from "../src/components/admin-config/WorkflowBuilderActionEditor";
import type { WorkflowBuilderNodeDraft } from "../src/hooks/adminWorkflowBuilderModel";

function makeNode(
  partial: Partial<WorkflowBuilderNodeDraft> & Pick<WorkflowBuilderNodeDraft, "id" | "nodeType">
): WorkflowBuilderNodeDraft {
  return {
    nodeKey: "step1",
    title: "Schritt 1",
    sortOrder: "1",
    positionX: null,
    positionY: null,
    configText: "",
    actions: [],
    ...partial,
  };
}

const baseProps = {
  actionDefinitions: [],
  automationPropertyCatalog: null,
  answerDefinitions: [],
  canManageAdvanced: true,
  onAddAction: vi.fn(),
  onUpdateAction: vi.fn(),
  onRemoveAction: vi.fn(),
};

describe("WorkflowBuilderActionEditor reorder", () => {
  it("swaps executionOrder of adjacent actions when middle action up-button is clicked", () => {
    const onUpdateAction = vi.fn();
    const node = makeNode({
      id: "n1",
      nodeType: "task",
      // three actions so that a2 (index 1) has both up/down enabled
      actions: [
        { id: "a1", actionKey: "act_a", inputMappingText: "", executionOrder: "1", onErrorBehavior: "abort" },
        { id: "a2", actionKey: "act_b", inputMappingText: "", executionOrder: "2", onErrorBehavior: "abort" },
        { id: "a3", actionKey: "act_c", inputMappingText: "", executionOrder: "3", onErrorBehavior: "abort" },
      ],
    });

    render(
      <WorkflowBuilderActionEditor
        {...baseProps}
        node={node}
        onUpdateAction={onUpdateAction}
      />
    );

    // Three "Nach oben" buttons: index 0 is disabled (a1), index 1 belongs to a2
    const upButtons = screen.getAllByLabelText("Nach oben");
    fireEvent.click(upButtons[1]!); // a2 moves up, swapping with a1

    expect(onUpdateAction).toHaveBeenCalledTimes(2);
    const callArgs = onUpdateAction.mock.calls as [string, { executionOrder: string }][];
    const callMap = new Map(callArgs.map(([id, patch]) => [id, patch.executionOrder]));
    expect(callMap.get("a2")).toBe("1"); // a2 gets a1's order
    expect(callMap.get("a1")).toBe("2"); // a1 gets a2's order
  });

  it("does not call onUpdateAction when first action up-button fires (disabled guard)", () => {
    const onUpdateAction = vi.fn();
    const node = makeNode({
      id: "n1",
      nodeType: "task",
      actions: [
        { id: "a1", actionKey: "act_a", inputMappingText: "", executionOrder: "1", onErrorBehavior: "abort" },
        { id: "a2", actionKey: "act_b", inputMappingText: "", executionOrder: "2", onErrorBehavior: "abort" },
      ],
    });

    render(
      <WorkflowBuilderActionEditor
        {...baseProps}
        node={node}
        onUpdateAction={onUpdateAction}
      />
    );

    // First "Nach oben" is disabled but jsdom still fires the event
    // The component's moveAction guard (targetIndex < 0) must prevent the update
    const upButtons = screen.getAllByLabelText("Nach oben");
    fireEvent.click(upButtons[0]!);

    expect(onUpdateAction).not.toHaveBeenCalled();
  });

  it("calls onRemoveAction with the correct actionId when remove button is clicked", () => {
    const onRemoveAction = vi.fn();
    const node = makeNode({
      id: "n1",
      nodeType: "task",
      actions: [
        { id: "a1", actionKey: "act_a", inputMappingText: "", executionOrder: "1", onErrorBehavior: "abort" },
      ],
    });

    render(
      <WorkflowBuilderActionEditor
        {...baseProps}
        node={node}
        onRemoveAction={onRemoveAction}
      />
    );

    fireEvent.click(screen.getByLabelText("Aktion entfernen"));

    expect(onRemoveAction).toHaveBeenCalledWith("a1");
  });
});
