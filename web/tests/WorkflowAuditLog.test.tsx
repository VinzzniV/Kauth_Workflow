import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import WorkflowAuditLog from "../src/components/workflow-detail/WorkflowAuditLog";
import type { WorkflowAuditEntry } from "../src/types/workflow";

function createAuditEntry(overrides: Partial<WorkflowAuditEntry> = {}): WorkflowAuditEntry {
  return {
    id: 1,
    eventType: "task_status_changed",
    createdAt: "2026-03-24T09:30:00.000Z",
    taskId: 11,
    taskKey: "hardware_setup",
    taskTitle: "Hardware einrichten",
    actorUserId: 7,
    actorUserName: "Admin Demo",
    oldValue: "ready",
    newValue: "in_progress",
    detail: "Task: Hardware einrichten",
    ...overrides,
  };
}

describe("WorkflowAuditLog", () => {
  it("renders translated status changes, tasks_generated, date grouping, and task comment details", () => {
    const firstDateLabel = new Date("2026-03-24T09:30:00.000Z").toLocaleDateString("de-DE", {
      dateStyle: "full",
    });
    const secondDateLabel = new Date("2026-03-23T07:00:00.000Z").toLocaleDateString("de-DE", {
      dateStyle: "full",
    });

    render(
      <WorkflowAuditLog
        entries={[
          createAuditEntry(),
          createAuditEntry({
            id: 2,
            eventType: "tasks_generated",
            createdAt: "2026-03-24T08:00:00.000Z",
            taskId: null,
            taskKey: null,
            taskTitle: null,
            oldValue: null,
            newValue: null,
            detail: "5 Aufgabe(n) initial erstellt",
          }),
          createAuditEntry({
            id: 3,
            eventType: "task_comment_added",
            createdAt: "2026-03-23T07:00:00.000Z",
            oldValue: null,
            newValue: null,
            detail: "Kommentartext",
          }),
          createAuditEntry({
            id: 4,
            eventType: "workflow_status_changed",
            createdAt: "2026-03-23T06:00:00.000Z",
            taskId: null,
            taskKey: null,
            taskTitle: null,
            oldValue: "waiting_for_supervisor",
            newValue: "waiting_for_department",
            detail: "Anforderungen gespeichert: 6",
          }),
        ]}
        isLoading={false}
        error={null}
      />
    );

    expect(screen.getByText("Aufgaben generiert")).toBeTruthy();
    expect(screen.getByText("Task-Status geändert")).toBeTruthy();
    expect(screen.getByText("Kommentar hinzugefügt")).toBeTruthy();
    expect(screen.getAllByText(firstDateLabel)).toHaveLength(1);
    expect(screen.getAllByText(secondDateLabel)).toHaveLength(1);
    expect(screen.getByText("Akteur: Admin Demo | Task-Key: hardware_setup | Bereit -> In Bearbeitung")).toBeTruthy();
    expect(screen.getByText("Kommentartext")).toBeTruthy();
    expect(screen.getByText("Akteur: Admin Demo | Wartet auf Abteilungsleitung -> Wartet auf Abteilung")).toBeTruthy();
  });

  it("does not duplicate workflow-level detail as a secondary note", () => {
    render(
      <WorkflowAuditLog
        entries={[
          createAuditEntry({
            id: 2,
            eventType: "tasks_generated",
            taskId: null,
            taskKey: null,
            taskTitle: null,
            oldValue: null,
            newValue: null,
            detail: "5 Aufgabe(n) initial erstellt",
          }),
        ]}
        isLoading={false}
        error={null}
      />
    );

    expect(screen.getAllByText("5 Aufgabe(n) initial erstellt")).toHaveLength(1);
  });
});
