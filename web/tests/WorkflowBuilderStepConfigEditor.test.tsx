import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderStepConfigEditor } from "../src/components/admin-config/WorkflowBuilderStepConfigEditor";
import type { AdminResponsibilityOwner } from "../src/types/auth";
import type { WorkflowBuilderNodeDraft } from "../src/hooks/adminWorkflowBuilderModel";

const RESPONSIBILITIES: AdminResponsibilityOwner[] = [
  {
    responsibilityId: 12,
    responsibilityKey: "it_admin",
    systemKey: null,
    responsibilityName: "IT Admin",
    responsibilityType: "process",
    departmentId: null,
    departmentName: null,
    appUserId: null,
    appUserDisplayName: null,
    updatedAt: null,
  },
];

function makeNode(
  partial: Partial<WorkflowBuilderNodeDraft> & Pick<WorkflowBuilderNodeDraft, "nodeType" | "id">
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

describe("WorkflowBuilderStepConfigEditor (LA5)", () => {
  it("renders responsibility select for any node type", () => {
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({ id: "n1", nodeType: "form" })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={vi.fn()}
      />
    );

    screen.getByLabelText(/Zuständig/);
  });

  it("emits configText with responsibilityKey when selecting a responsibility", () => {
    const onUpdate = vi.fn();
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({ id: "n1", nodeType: "task" })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={onUpdate}
      />
    );

    fireEvent.change(screen.getByLabelText(/Zuständig/), { target: { value: "it_admin" } });

    expect(onUpdate).toHaveBeenCalledTimes(1);
    const patch = onUpdate.mock.calls[0]![0];
    const parsedConfig = JSON.parse(patch.configText);
    expect(parsedConfig.responsibilityKey).toBe("it_admin");
  });

  it("shows LA5 hint instead of legacyTemplateKey dropdown for task nodes", () => {
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({ id: "n1", nodeType: "task" })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={vi.fn()}
      />
    );

    screen.getByText(/LA5: Aufgabe\/Freigabe-Spezifikation/i);
    // Confirm: NO template dropdown is rendered for task/approval
    expect(screen.queryByText(/Aufgabenvorlage/i)).toBeNull();
    expect(screen.queryByText(/Freigabevorlage/i)).toBeNull();
  });

  it("shows LA5 hint also for approval nodes", () => {
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({ id: "n1", nodeType: "approval" })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={vi.fn()}
      />
    );

    screen.getByText(/LA5: Aufgabe\/Freigabe-Spezifikation/i);
  });

  it("does not show LA5 hint for form nodes", () => {
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({ id: "n1", nodeType: "form" })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={vi.fn()}
      />
    );

    expect(screen.queryByText(/LA5: Aufgabe\/Freigabe-Spezifikation/i)).toBeNull();
  });

  it("preserves existing config keys when editing notification label", () => {
    const onUpdate = vi.fn();
    render(
      <WorkflowBuilderStepConfigEditor
        node={makeNode({
          id: "n1",
          nodeType: "task",
          configText: JSON.stringify({ responsibilityKey: "it_admin", summaryText: "Bestand" }),
        })}
        responsibilityOwners={RESPONSIBILITIES}
        taskTemplates={[]}
        answerDefinitions={[]}
        onUpdate={onUpdate}
      />
    );

    fireEvent.change(screen.getByLabelText(/Benachrichtigte/), { target: { value: "HR-Team" } });

    const patch = onUpdate.mock.calls[0]![0];
    const parsed = JSON.parse(patch.configText);
    expect(parsed.responsibilityKey).toBe("it_admin");
    expect(parsed.summaryText).toBe("Bestand");
    expect(parsed.notificationLabel).toBe("HR-Team");
  });
});
