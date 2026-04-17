import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { within } from "@testing-library/react";
import RotationTaskDetailPage from "../src/pages/RotationTaskDetailPage";
import * as taskApi from "../src/services/taskApi";
import * as rotationQueries from "../src/services/queries/rotationQueries";
import { createRotationTask, renderWithApp } from "./testUtils";
import type { RotationAuditEntry, RotationGeneratedTask, RotationNotification, RotationPlanDetail } from "../src/types/rotation";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useParams: () => ({ taskRef: "rot:1" }),
  };
});

vi.mock("../src/services/taskApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/taskApi")>("../src/services/taskApi");
  return {
    ...actual,
    getTaskByRef: vi.fn(),
    updateTaskStatusByRef: vi.fn(),
    addTaskCommentByRef: vi.fn(),
  };
});

vi.mock("../src/services/queries/rotationQueries", async () => {
  const actual = await vi.importActual<typeof import("../src/services/queries/rotationQueries")>(
    "../src/services/queries/rotationQueries"
  );
  return {
    ...actual,
    useRotationPlanDetail: vi.fn(),
    useRotationGeneratedTasks: vi.fn(),
    useRotationAuditLog: vi.fn(),
    useRotationNotifications: vi.fn(),
  };
});

const mockedGetTaskByRef = vi.mocked(taskApi.getTaskByRef);
const mockedUpdateTaskStatusByRef = vi.mocked(taskApi.updateTaskStatusByRef);
const mockedUseRotationPlanDetail = vi.mocked(rotationQueries.useRotationPlanDetail);
const mockedUseRotationGeneratedTasks = vi.mocked(rotationQueries.useRotationGeneratedTasks);
const mockedUseRotationAuditLog = vi.mocked(rotationQueries.useRotationAuditLog);
const mockedUseRotationNotifications = vi.mocked(rotationQueries.useRotationNotifications);

function createPlanDetail(): RotationPlanDetail {
  return {
    id: 42,
    personId: 11,
    sourceWorkflowUid: "wf-onboarding-1",
    displayName: "Anika Sattler",
    firstName: "Anika",
    lastName: "Sattler",
    departmentId: 3,
    departmentName: "BS",
    title: "Anika Sattler - Durchlauf 2026",
    status: "active",
    createdByUserId: 1,
    createdAt: "2026-06-01T08:00:00.000Z",
    updatedAt: "2026-06-10T08:00:00.000Z",
    stations: [
      {
        id: 99,
        rotationPlanId: 42,
        departmentId: 3,
        departmentName: "BS",
        startDate: "2026-06-01",
        endDate: "2026-06-19",
        orderIndex: 0,
        location: null,
        notes: null,
        status: "completed",
        createdAt: "2026-06-01T08:00:00.000Z",
        updatedAt: "2026-06-01T08:00:00.000Z",
      },
      {
        id: 100,
        rotationPlanId: 42,
        departmentId: 9,
        departmentName: "IT",
        startDate: "2026-06-20",
        endDate: "2026-07-01",
        orderIndex: 1,
        location: null,
        notes: null,
        status: "planned",
        createdAt: "2026-06-01T08:00:00.000Z",
        updatedAt: "2026-06-01T08:00:00.000Z",
      },
    ],
  };
}

function createGeneratedTask(): RotationGeneratedTask {
  return {
    id: 1,
    taskRef: "rot:1",
    rotationPlanId: 42,
    rotationStationId: 100,
    personId: 11,
    departmentId: 9,
    departmentName: "IT",
    templateId: 7,
    templateTitle: "Rechte setzen",
    triggerType: "enter",
    anchorDate: "2026-06-20",
    title: "Rechte setzen",
    description: "Zugriff vorbereiten",
    taskType: "technical",
    responsibilityId: 10,
    responsibilityName: "IT",
    dueDate: "2026-06-18",
    status: "open",
    completionNote: null,
    startedAt: null,
    completedAt: null,
    createdAt: "2026-06-10T08:00:00.000Z",
    updatedAt: "2026-06-10T08:00:00.000Z",
    assignments: [],
    comments: [],
  };
}

function createAuditEntry(): RotationAuditEntry {
  return {
    id: 10,
    rotationPlanId: 42,
    rotationStationId: 100,
    generatedTaskId: 1,
    actorUserId: 7,
    actorUserName: "IT",
    eventType: "rotation_task_status_changed",
    oldValue: { status: "open" },
    newValue: { status: "completed" },
    detail: "Rechte setzen",
    createdAt: "2026-06-10T10:00:00.000Z",
  };
}

function createNotification(): RotationNotification {
  return {
    id: 88,
    rotationPlanId: 42,
    rotationStationId: 100,
    generatedTaskId: 1,
    notificationType: "reminder",
    recipientEmail: "it@example.com",
    recipientName: "IT",
    recipientUserId: 7,
    subject: "Faellige Rotationsaufgaben",
    payload: {
      recipientName: "IT",
      personDisplayName: "Anika Sattler",
      nextDepartmentName: "IT",
    },
    status: "sent",
    attempts: 1,
    lastError: null,
    sentAt: "2026-06-18T08:00:00.000Z",
    createdAt: "2026-06-18T07:55:00.000Z",
  };
}

describe("RotationTaskDetailPage", () => {
  beforeEach(() => {
    mockedGetTaskByRef.mockReset();
    mockedUpdateTaskStatusByRef.mockReset();
    mockedUseRotationPlanDetail.mockReset();
    mockedUseRotationGeneratedTasks.mockReset();
    mockedUseRotationAuditLog.mockReset();
    mockedUseRotationNotifications.mockReset();

    mockedGetTaskByRef.mockResolvedValue(createRotationTask());
    mockedUseRotationPlanDetail.mockReturnValue({
      data: createPlanDetail(),
      isLoading: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);
    mockedUseRotationGeneratedTasks.mockReturnValue({
      data: [createGeneratedTask()],
      isLoading: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);
    mockedUseRotationAuditLog.mockReturnValue({
      data: [createAuditEntry()],
      isLoading: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);
    mockedUseRotationNotifications.mockReturnValue({
      data: [createNotification()],
      isLoading: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);
  });

  it("shows current and next station data for the selected rotation task", async () => {
    renderWithApp(<RotationTaskDetailPage />, { roleKeys: ["auth_worker"] });

    const contextHeading = await screen.findByRole("heading", { name: "Wechselkontext" });
    const contextPanel = contextHeading.closest("section");
    expect(contextPanel).toBeTruthy();
    const scoped = within(contextPanel!);
    expect(scoped.getByText("BS")).toBeTruthy();
    expect(scoped.getByText("IT")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Generierte Maßnahmen derselben Station" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Historie" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Benachrichtigungshistorie" })).toBeTruthy();
  });

  it("updates the task status via taskRef on the detail page", async () => {
    mockedUpdateTaskStatusByRef.mockResolvedValue(createRotationTask({ status: "completed" }));

    renderWithApp(<RotationTaskDetailPage />, { roleKeys: ["auth_worker"] });

    fireEvent.change(await screen.findByLabelText("Status"), {
      target: { value: "done" },
    });

    await waitFor(() => {
      expect(mockedUpdateTaskStatusByRef).toHaveBeenCalledWith("rot:1", "completed");
    });
  });
});
