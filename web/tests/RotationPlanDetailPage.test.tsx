import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import RotationPlanDetailPage from "../src/pages/RotationPlanDetailPage";
import * as rotationApi from "../src/services/rotationApi";
import * as rotationQueries from "../src/services/queries/rotationQueries";
import * as roleQueries from "../src/services/queries/roleQueries";
import { renderWithApp } from "./testUtils";
import type {
  RotationAuditEntry,
  RotationGeneratedTask,
  RotationNotification,
  RotationPlanDetail,
} from "../src/types/rotation";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useParams: () => ({ planId: "42" }),
  };
});

vi.mock("../src/services/rotationApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/rotationApi")>(
    "../src/services/rotationApi"
  );
  return {
    ...actual,
    createRotationStation: vi.fn(),
    updateRotationStation: vi.fn(),
    deleteRotationStation: vi.fn(),
    regenerateRotationGeneratedTasks: vi.fn(),
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

vi.mock("../src/services/queries/roleQueries", async () => {
  const actual = await vi.importActual<typeof import("../src/services/queries/roleQueries")>(
    "../src/services/queries/roleQueries"
  );
  return {
    ...actual,
    useDepartments: vi.fn(),
  };
});

const mockedCreateRotationStation = vi.mocked(rotationApi.createRotationStation);
const mockedRegenerateRotationGeneratedTasks = vi.mocked(rotationApi.regenerateRotationGeneratedTasks);
const mockedUseRotationPlanDetail = vi.mocked(rotationQueries.useRotationPlanDetail);
const mockedUseRotationGeneratedTasks = vi.mocked(rotationQueries.useRotationGeneratedTasks);
const mockedUseRotationAuditLog = vi.mocked(rotationQueries.useRotationAuditLog);
const mockedUseRotationNotifications = vi.mocked(rotationQueries.useRotationNotifications);
const mockedUseDepartments = vi.mocked(roleQueries.useDepartments);

function createPlanDetail(overrides: Partial<RotationPlanDetail> = {}): RotationPlanDetail {
  return {
    id: 42,
    personId: 11,
    displayName: "Anika Sattler",
    departmentName: "BS",
    title: "Anika Sattler - Durchlauf 2026",
    status: "active",
    sourceWorkflowUid: "wf-onboarding-1",
    createdAt: "2026-06-05T08:00:00.000Z",
    updatedAt: "2026-06-06T08:00:00.000Z",
    stations: [
      {
        id: 100,
        rotationPlanId: 42,
        departmentId: 3,
        departmentName: "BS",
        startDate: "2026-06-01",
        endDate: "2026-06-20",
        orderIndex: 0,
        location: "Werk A",
        notes: "Startstation",
        status: "planned",
        createdAt: "2026-06-05T08:00:00.000Z",
        updatedAt: "2026-06-05T08:00:00.000Z",
      },
    ],
    ...overrides,
  };
}

function createGeneratedTask(overrides: Partial<RotationGeneratedTask> = {}): RotationGeneratedTask {
  return {
    id: 500,
    taskRef: "rot:500",
    rotationPlanId: 42,
    rotationStationId: 100,
    personId: 11,
    departmentId: 3,
    departmentName: "BS",
    templateId: 8,
    templateTitle: "Ordnerrechte setzen",
    title: "Ordnerrechte setzen",
    description: "Zugriffe vorbereiten",
    taskType: "technical",
    triggerType: "enter",
    anchorDate: "2026-06-01",
    dueDate: "2026-05-29",
    responsibilityId: 4,
    responsibilityName: "IT",
    status: "open",
    completionNote: null,
    completedAt: null,
    createdAt: "2026-06-05T08:00:00.000Z",
    updatedAt: "2026-06-05T08:00:00.000Z",
    assignments: [],
    comments: [],
    ...overrides,
  };
}

function createAuditEntry(overrides: Partial<RotationAuditEntry> = {}): RotationAuditEntry {
  return {
    id: 1,
    rotationPlanId: 42,
    rotationStationId: 100,
    generatedTaskId: 500,
    actorUserId: 99,
    actorUserName: "HR",
    eventType: "rotation_station_created",
    oldValue: null,
    newValue: { status: "planned" },
    detail: "BS",
    createdAt: "2026-06-05T08:10:00.000Z",
    ...overrides,
  };
}

function createNotification(overrides: Partial<RotationNotification> = {}): RotationNotification {
  return {
    id: 77,
    rotationPlanId: 42,
    rotationStationId: 100,
    generatedTaskId: 500,
    notificationType: "upcoming_change",
    recipientEmail: "it@example.com",
    recipientName: "IT",
    recipientUserId: 5,
    subject: "Bevorstehender Wechsel",
    payload: {
      recipientName: "IT",
      personDisplayName: "Anika Sattler",
      nextDepartmentName: "BS",
    },
    status: "sent",
    attempts: 1,
    lastError: null,
    sentAt: "2026-05-30T08:00:00.000Z",
    createdAt: "2026-05-30T07:55:00.000Z",
    ...overrides,
  };
}

describe("RotationPlanDetailPage", () => {
  beforeEach(() => {
    mockedCreateRotationStation.mockReset();
    mockedRegenerateRotationGeneratedTasks.mockReset();
    mockedUseRotationPlanDetail.mockReset();
    mockedUseRotationGeneratedTasks.mockReset();
    mockedUseRotationAuditLog.mockReset();
    mockedUseRotationNotifications.mockReset();
    mockedUseDepartments.mockReset();

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
    mockedUseDepartments.mockReturnValue({
      data: [
        { id: 3, name: "BS" },
        { id: 9, name: "IT" },
      ],
      isLoading: false,
      error: null,
    } as never);
  });

  it("renders stations and generated tasks for the selected plan", async () => {
    renderWithApp(<RotationPlanDetailPage />, { roleKeys: ["auth_hr"] });

    expect(await screen.findByRole("heading", { name: "Planübersicht" })).toBeTruthy();
    expect(screen.getByText("Anika Sattler")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Stationen" })).toBeTruthy();
    expect(screen.getByText("1. BS")).toBeTruthy();
    const taskSection = screen.getByRole("heading", { name: "Generierte Maßnahmen" }).closest("section");
    expect(taskSection).toBeTruthy();
    expect(
      within(taskSection ?? document.body).getByRole("heading", { name: "Ordnerrechte setzen" })
    ).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Verlauf / Audit" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Benachrichtigungen" })).toBeTruthy();
  });

  it("creates a station and can trigger task regeneration", async () => {
    mockedCreateRotationStation.mockResolvedValue({
      id: 101,
      rotationPlanId: 42,
      departmentId: 9,
      departmentName: "IT",
      startDate: "2026-06-21",
      endDate: "2026-06-30",
      orderIndex: 1,
      location: null,
      notes: null,
      status: "planned",
      createdAt: "2026-06-06T08:00:00.000Z",
      updatedAt: "2026-06-06T08:00:00.000Z",
    });
    mockedRegenerateRotationGeneratedTasks.mockResolvedValue({
      created: 1,
      updated: 2,
      cancelled: 0,
      unchanged: 3,
    });

    renderWithApp(<RotationPlanDetailPage />, { roleKeys: ["auth_hr"] });

    fireEvent.change(await screen.findByLabelText("Abteilung"), { target: { value: "9" } });
    fireEvent.change(screen.getByLabelText("Startdatum"), { target: { value: "2026-06-21" } });
    fireEvent.change(screen.getByLabelText("Enddatum"), { target: { value: "2026-06-30" } });
    fireEvent.click(screen.getByRole("button", { name: "Station anlegen" }));

    await waitFor(() => {
      expect(mockedCreateRotationStation).toHaveBeenCalledWith(42, {
        departmentId: 9,
        startDate: "2026-06-21",
        endDate: "2026-06-30",
        orderIndex: 0,
        location: undefined,
        notes: undefined,
        status: "planned",
      });
    });

    fireEvent.click(screen.getByRole("button", { name: "Tasks neu synchronisieren" }));

    await waitFor(() => {
      expect(mockedRegenerateRotationGeneratedTasks).toHaveBeenCalledWith(42);
    });
  });
});
