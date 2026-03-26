import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import MyTasksPage from "../src/pages/MyTasksPage";
import * as lifecycleApi from "../src/services/lifecycleApi";
import { createTaskWithWorkflow, renderWithApp } from "./testUtils";

vi.mock("../src/services/lifecycleApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/lifecycleApi")>("../src/services/lifecycleApi");
  return {
    ...actual,
    getMyTasks: vi.fn(),
    updateTaskStatus: vi.fn(),
  };
});

const mockedGetMyTasks = vi.mocked(lifecycleApi.getMyTasks);

describe("MyTasksPage", () => {
  beforeEach(() => {
    mockedGetMyTasks.mockReset();
  });

  it("filters visible tasks by the selected task status", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createTaskWithWorkflow({
        title: "Notebook vorbereiten",
        description: "Notebook bereitstellen",
        status: "open",
      }),
      createTaskWithWorkflow(
        {
          id: 2,
          taskKey: "account_setup",
          title: "Zugang einrichten",
          description: "Microsoft-365-Konto aktivieren",
          status: "in_progress",
        },
        {
          workflowUid: "wf-2",
          firstName: "Ben",
          lastName: "Beispiel",
        }
      ),
    ]);

    renderWithApp(<MyTasksPage />, { roleKeys: ["auth_worker"] });

    expect(await screen.findByRole("heading", { name: "Notebook vorbereiten" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Zugang einrichten" })).toBeTruthy();

    fireEvent.change(screen.getByLabelText("Status"), { target: { value: "in_progress" } });

    const visibleTaskCard = await screen.findByRole("heading", { name: "Zugang einrichten" });
    expect(within(visibleTaskCard.closest("article") ?? document.body).getByText("Microsoft-365-Konto aktivieren")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Notebook vorbereiten" })).toBeNull();
  });
});
