import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import MyTasksPage from "../src/pages/MyTasksPage";
import * as taskApi from "../src/services/taskApi";
import { createTaskWithWorkflow, renderWithApp } from "./testUtils";

vi.mock("../src/services/taskApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/taskApi")>("../src/services/taskApi");
  return {
    ...actual,
    getMyTasks: vi.fn(),
    updateTaskStatus: vi.fn(),
    addTaskComment: vi.fn(),
  };
});

const mockedGetMyTasks = vi.mocked(taskApi.getMyTasks);

describe("MyTasksPage", () => {
  beforeEach(() => {
    mockedGetMyTasks.mockReset();
  });

  it("shows workflow summary cards in the overview", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createTaskWithWorkflow({
        title: "Notebook vorbereiten",
        status: "open",
      }),
      createTaskWithWorkflow(
        {
          id: 2,
          taskKey: "account_setup",
          title: "Zugang einrichten",
          status: "in_progress",
        },
        { workflowUid: "wf-2", firstName: "Ben", lastName: "Beispiel" }
      ),
    ]);

    renderWithApp(<MyTasksPage />, { roleKeys: ["auth_worker"] });

    // Overview shows workflow summary cards, not individual task headings
    const list = await screen.findByRole("generic", { name: "Vorgänge mit Aufgaben" });
    expect(list).toBeTruthy();
    // Both tasks are for different workflows → two summary cards
    expect(within(list).getAllByRole("button")).toHaveLength(2);
  });

  it("offers a table mode for workflow task summaries", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createTaskWithWorkflow({
        title: "Notebook vorbereiten",
        status: "open",
      }),
    ]);

    renderWithApp(<MyTasksPage />, { roleKeys: ["auth_worker"] });

    expect(await screen.findByRole("generic", { name: "Vorgänge mit Aufgaben" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Tabelle" }));

    expect(screen.getByRole("table", { name: "Tabellenansicht Vorgänge mit Aufgaben" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Offen" })).toBeTruthy();
  });

  it("groups tasks by their visible status buckets after selecting a workflow", async () => {
    // All three tasks belong to the same workflow (wf-1 default)
    mockedGetMyTasks.mockResolvedValue([
      createTaskWithWorkflow({
        title: "Notebook vorbereiten",
        status: "ready",
      }),
      createTaskWithWorkflow(
        {
          id: 2,
          taskKey: "account_setup",
          title: "Zugang einrichten",
          status: "in_progress",
        }
      ),
      createTaskWithWorkflow(
        {
          id: 3,
          taskKey: "archive",
          title: "Archivieren",
          status: "done",
        }
      ),
    ]);

    renderWithApp(<MyTasksPage />, { roleKeys: ["auth_worker"] });

    // Click on the single workflow summary card to enter detail view
    const summaryCard = await screen.findByRole("button", { name: /Alice Example/i });
    fireEvent.click(summaryCard);

    expect(screen.getByLabelText("Vorgangs-Auswahl")).toBeTruthy();
    expect(screen.getByLabelText("Aufgaben für Alice Example")).toBeTruthy();
    // Detail view shows task groups by status
    expect(await screen.findByRole("heading", { name: "Offen" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "In Bearbeitung" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Erledigt" })).toBeTruthy();
  });
});
