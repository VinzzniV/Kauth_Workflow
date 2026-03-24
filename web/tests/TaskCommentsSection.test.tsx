import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import TaskCommentsSection from "../src/components/workflows/TaskCommentsSection";
import type { WorkflowTask } from "../src/types/workflow";

function createTask(overrides: Partial<WorkflowTask> = {}): WorkflowTask {
  return {
    id: 15,
    taskTemplateId: 3,
    taskKey: "hardware_setup",
    title: "Hardware einrichten",
    description: "Laptop und Zubehoer bereitstellen",
    category: "it",
    iconKey: "laptop",
    status: "ready",
    isRequired: true,
    sortOrder: 1,
    createdAt: "2026-03-24T08:00:00.000Z",
    dueInDays: 5,
    dueAt: "2026-03-29T08:00:00.000Z",
    slaStatus: "on_track",
    readyAt: null,
    startedAt: null,
    completedAt: null,
    cancelledAt: null,
    processArea: "IT",
    isDepartmentPhaseTask: true,
    canUpdateStatus: true,
    canAddComment: true,
    assignments: [],
    dependencies: [],
    comments: [],
    ...overrides,
  };
}

describe("TaskCommentsSection", () => {
  it("shows latest activity, character count, and keeps the panel open while a draft exists", () => {
    const task = createTask({
      comments: [
        {
          id: 8,
          taskId: 15,
          authorUserId: 21,
          authorUserName: "Max Mustermann",
          commentText: "Bitte VPN-Zugang vorab pruefen.",
          createdAt: "2026-03-24T09:30:00.000Z",
        },
      ],
    });

    const { container } = render(
      <TaskCommentsSection
        task={task}
        draftValue="Rueckfrage zum Liefertermin"
        isSaving={false}
        feedbackMessage={null}
        onDraftChange={vi.fn()}
        onSubmit={vi.fn(async () => undefined)}
      />
    );

    expect(screen.getByText(/Letzte Aktivität:\s*Max Mustermann/)).toBeTruthy();
    expect(screen.getByText("1 Eintrag zur Aufgabe.")).toBeTruthy();
    expect(screen.getByText("Entwurf offen")).toBeTruthy();
    expect(screen.getByText("27/2000 Zeichen")).toBeTruthy();
    expect(container.querySelector("details")?.open).toBe(true);
  });

  it("shows contextual empty-state guidance and save feedback", () => {
    render(
      <TaskCommentsSection
        task={createTask()}
        draftValue=""
        isSaving={false}
        feedbackMessage="Kommentar wurde gespeichert."
        onDraftChange={vi.fn()}
        onSubmit={vi.fn(async () => undefined)}
      />
    );

    expect(screen.getByText("Noch keine Kommentare. Rückfragen direkt an der Aufgabe dokumentieren.")).toBeTruthy();
    expect(screen.getByText("Noch keine Kommentare vorhanden.")).toBeTruthy();
    expect(screen.getByRole("status").textContent).toContain("Kommentar wurde gespeichert.");
  });
});
