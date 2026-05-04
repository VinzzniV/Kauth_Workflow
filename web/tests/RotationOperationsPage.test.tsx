import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { within } from "@testing-library/react";
import RotationOperationsPage from "../src/pages/RotationOperationsPage";
import * as taskApi from "../src/services/taskApi";
import { createRotationTask, renderWithApp } from "./testUtils";

vi.mock("../src/services/taskApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/taskApi")>("../src/services/taskApi");
  return {
    ...actual,
    getMyTasks: vi.fn(),
    updateTaskStatusByRef: vi.fn(),
  };
});

const mockedGetMyTasks = vi.mocked(taskApi.getMyTasks);
const mockedUpdateTaskStatusByRef = vi.mocked(taskApi.updateTaskStatusByRef);

describe("RotationOperationsPage", () => {
  beforeEach(() => {
    mockedGetMyTasks.mockReset();
    mockedUpdateTaskStatusByRef.mockReset();
  });

  it("shows upcoming changes and grouped person entries for rotation tasks", async () => {
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

    mockedGetMyTasks.mockResolvedValue([
      createRotationTask(
        undefined,
        {
          anchorDate: tomorrow,
        }
      ),
      createRotationTask(
        {
          id: 2,
          title: "Hardware bereitstellen",
          status: "in_progress",
        },
        {
          rotationPlanId: 42,
          anchorDate: tomorrow,
        }
      ),
    ]);

    renderWithApp(<RotationOperationsPage />, { roleKeys: ["auth_worker"] });

    const upcomingHeading = await screen.findByRole("heading", { name: "Kommende Wechsel" });
    const upcomingSection = upcomingHeading.closest("section");
    expect(upcomingSection).toBeTruthy();
    expect(within(upcomingSection!).getByText("Anika Sattler")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Aufgaben nach Person" })).toBeTruthy();
    expect(screen.getAllByRole("link", { name: "Detail öffnen" }).length).toBeGreaterThan(0);
  });

  it("offers a table mode for rotation task triage", async () => {
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

    mockedGetMyTasks.mockResolvedValue([
      createRotationTask(undefined, {
        anchorDate: tomorrow,
      }),
    ]);

    renderWithApp(<RotationOperationsPage />, { roleKeys: ["auth_worker"] });

    expect(await screen.findByLabelText("Rotationsaufgabenliste")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Tabelle" }));

    expect(screen.getByRole("table", { name: "Tabellenansicht Rotationsaufgaben" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Wechselbezug" })).toBeTruthy();
  });

  it("updates a rotation task status via taskRef", async () => {
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

    mockedGetMyTasks.mockResolvedValue([
      createRotationTask(
        undefined,
        {
          anchorDate: tomorrow,
        }
      ),
    ]);
    mockedUpdateTaskStatusByRef.mockResolvedValue(
      createRotationTask({ status: "completed" })
    );

    renderWithApp(<RotationOperationsPage />, { roleKeys: ["auth_worker"] });

    const taskList = await screen.findByLabelText("Rotationsaufgabenliste");
    fireEvent.change(within(taskList).getByLabelText("Status"), {
      target: { value: "done" },
    });

    await waitFor(() => {
      expect(mockedUpdateTaskStatusByRef).toHaveBeenCalledWith("rot:1", "completed");
    });
  });
});
