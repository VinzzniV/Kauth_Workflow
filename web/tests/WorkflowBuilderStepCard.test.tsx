import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderStepCard } from "../src/components/admin-config/WorkflowBuilderStepCard";
import type {
  WorkflowBuilderNodeDraft,
  WorkflowBuilderVersionDraft,
} from "../src/hooks/adminWorkflowBuilderModel";

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
    actions: [], specs: [],
    ...partial,
  };
}

const VERSION_DRAFT: WorkflowBuilderVersionDraft = {
  name: "Draft",
  description: "",
  primaryLegacyProcessTypeKey: "",
  nodes: [],
  edges: [],
};

const baseProps = {
  versionDraft: VERSION_DRAFT,
  workflowDefinitions: [],
  actionDefinitions: [],
  automationPropertyCatalog: null,
  taskTemplates: [],
  answerDefinitions: [],
  taskTemplateConditions: [],
  taskTemplateDependencies: [],
  responsibilityOwners: [],
  canManageAdvanced: true,
  onUpdate: vi.fn(),
  onMoveUp: vi.fn(),
  onMoveDown: vi.fn(),
  onRemove: vi.fn(),
  onAddAction: vi.fn(),
  onUpdateAction: vi.fn(),
  onRemoveAction: vi.fn(),
};

describe("WorkflowBuilderStepCard", () => {
  it("fires onMoveUp / onMoveDown / onRemove when icon buttons are clicked", () => {
    const onMoveUp = vi.fn();
    const onMoveDown = vi.fn();
    const onRemove = vi.fn();
    render(
      <WorkflowBuilderStepCard
        {...baseProps}
        node={makeNode({ id: "n1", nodeType: "form" })}
        index={1}
        totalCount={3}
        onMoveUp={onMoveUp}
        onMoveDown={onMoveDown}
        onRemove={onRemove}
      />
    );

    fireEvent.click(screen.getByLabelText("Nach oben verschieben"));
    fireEvent.click(screen.getByLabelText("Nach unten verschieben"));
    fireEvent.click(screen.getByLabelText("Schritt entfernen"));

    expect(onMoveUp).toHaveBeenCalledTimes(1);
    expect(onMoveDown).toHaveBeenCalledTimes(1);
    expect(onRemove).toHaveBeenCalledTimes(1);
  });

  it("disables move-up at first position and move-down at last position", () => {
    render(
      <WorkflowBuilderStepCard
        {...baseProps}
        node={makeNode({ id: "n1", nodeType: "form" })}
        index={0}
        totalCount={1}
      />
    );

    expect((screen.getByLabelText("Nach oben verschieben") as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByLabelText("Nach unten verschieben") as HTMLButtonElement).disabled).toBe(true);
  });

  it("emits title patch when the title input changes", () => {
    const onUpdate = vi.fn();
    render(
      <WorkflowBuilderStepCard
        {...baseProps}
        node={makeNode({ id: "n1", nodeType: "form" })}
        index={0}
        totalCount={2}
        onUpdate={onUpdate}
      />
    );

    fireEvent.change(screen.getByLabelText(/Schritt 1 Name/), { target: { value: "Anforderungen erfassen" } });

    expect(onUpdate).toHaveBeenCalledWith({ title: "Anforderungen erfassen" });
  });

  it("toggles technical details and renders Lucide chevron icons (svg) instead of text symbols", () => {
    const { container } = render(
      <WorkflowBuilderStepCard
        {...baseProps}
        node={makeNode({ id: "n1", nodeType: "form" })}
        index={0}
        totalCount={2}
      />
    );

    // Move-Up/Down/Remove buttons each contain an SVG (Lucide icon)
    const moveUpBtn = screen.getByLabelText("Nach oben verschieben");
    expect(moveUpBtn.querySelector("svg")).not.toBeNull();

    // Tech-Toggle Button starts collapsed → ChevronRight rendered
    const techToggle = screen.getByText("Technische Details").closest("button")!;
    expect(techToggle.getAttribute("aria-expanded")).toBe("false");
    expect(techToggle.querySelector("svg")).not.toBeNull();

    fireEvent.click(techToggle);
    expect(techToggle.getAttribute("aria-expanded")).toBe("true");

    // No legacy text symbols leak into rendered output
    expect(container.textContent).not.toContain("▼");
    expect(container.textContent).not.toContain("►");
    expect(container.textContent).not.toContain("✕");
    expect(container.textContent).not.toContain("↑");
    expect(container.textContent).not.toContain("↓");
  });

  it("renders the typed step number prefix and a type label", () => {
    render(
      <WorkflowBuilderStepCard
        {...baseProps}
        node={makeNode({ id: "n1", nodeType: "approval" })}
        index={4}
        totalCount={6}
      />
    );

    screen.getByText("#5");
    // The type label is rendered via getWorkflowBuilderNodeTypeLabel — at minimum a non-empty span exists
    const typeLabel = document.querySelector(".wf-step-card-type");
    expect(typeLabel?.textContent?.trim().length ?? 0).toBeGreaterThan(0);
  });
});
