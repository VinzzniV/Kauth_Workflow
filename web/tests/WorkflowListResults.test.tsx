import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { WorkflowListResults } from "../src/pages/WorkflowListResults";
import type { WorkflowSummary } from "../src/types/workflow";

function createWorkflowSummary(overrides: Partial<WorkflowSummary> = {}): WorkflowSummary {
  return {
    uid: "wf-1",
    processType: { key: "onboarding", name: "Onboarding" },
    firstName: "Anna",
    lastName: "Müller",
    employeeNumber: 1001,
    badgeNumber: 42,
    departmentId: 1,
    departmentName: "IT",
    roleId: 2,
    roleName: "Entwickler",
    workflowStatus: "waiting_for_department",
    createdAt: "2026-01-01T10:00:00.000Z",
    completedAt: null,
    deadlineDate: null,
    archivedAt: null,
    pendingNotifications: 0,
    failedNotifications: 0,
    requirementSummary: { pendingVisibleCount: 0, totalVisibleCount: 0 },
    taskMetrics: {
      overall: { inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 1 },
      required: { inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 0 },
      departmentPhase: { inProgressCount: 0, blockedCount: 0, doneCount: 0, completedCount: 0, activeCount: 0 },
    },
    taskSummary: "",
    responsibilityOptions: [],
    ...overrides,
  };
}

function renderResults(rows: WorkflowSummary[]) {
  return render(
    <MemoryRouter>
      <WorkflowListResults rows={rows} isLoading={false} error={null} hasActiveFilters={false} onRetry={() => undefined} />
    </MemoryRouter>
  );
}

describe("WorkflowListResults — split workspace selection (Z18-F7)", () => {
  it("shows empty preview hint on initial render without auto-selecting first row", () => {
    renderResults([createWorkflowSummary()]);
    expect(screen.getByText("Vorgang auswählen")).toBeTruthy();
    expect(screen.getByText(/Wählen Sie links einen Vorgang aus/)).toBeTruthy();
  });

  it("shows workflow preview after user clicks Vorschau", () => {
    renderResults([createWorkflowSummary({ uid: "wf-1", firstName: "Anna", lastName: "Müller" })]);
    fireEvent.click(screen.getByRole("button", { name: "Vorschau" }));
    expect(screen.getByLabelText("Vorschau für Anna Müller")).toBeTruthy();
    // preview pane meta includes name
    expect(screen.getAllByText(/Anna Müller/).length).toBeGreaterThan(0);
  });

  it("resets selection when rows change and selected uid is no longer present", () => {
    const rowA = createWorkflowSummary({ uid: "wf-a", firstName: "Anna", lastName: "Müller" });
    const rowB = createWorkflowSummary({ uid: "wf-b", firstName: "Ben", lastName: "Meier" });
    const { rerender } = render(
      <MemoryRouter>
        <WorkflowListResults rows={[rowA]} isLoading={false} error={null} hasActiveFilters={false} onRetry={() => undefined} />
      </MemoryRouter>
    );

    // Select wf-a
    fireEvent.click(screen.getByRole("button", { name: "Vorschau" }));
    expect(screen.getByText("Vorgangs-Vorschau")).toBeTruthy();

    // Navigate to page 2 — only rowB present
    rerender(
      <MemoryRouter>
        <WorkflowListResults rows={[rowB]} isLoading={false} error={null} hasActiveFilters={false} onRetry={() => undefined} />
      </MemoryRouter>
    );

    // Selection should be cleared; empty hint should reappear
    expect(screen.getByText("Vorgang auswählen")).toBeTruthy();
  });

  it("keeps selection when rows are refreshed and selected uid still present", () => {
    const row = createWorkflowSummary({ uid: "wf-1" });
    const { rerender } = render(
      <MemoryRouter>
        <WorkflowListResults rows={[row]} isLoading={false} error={null} hasActiveFilters={false} onRetry={() => undefined} />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Vorschau" }));
    expect(screen.getByText("Vorgangs-Vorschau")).toBeTruthy();

    // Same rows (same uid) — selection must persist
    rerender(
      <MemoryRouter>
        <WorkflowListResults rows={[row]} isLoading={false} error={null} hasActiveFilters={false} onRetry={() => undefined} />
      </MemoryRouter>
    );

    expect(screen.getByText("Vorgangs-Vorschau")).toBeTruthy();
  });
});
