import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import RotationPlanningPage from "../src/pages/RotationPlanningPage";
import * as rotationApi from "../src/services/rotationApi";
import * as rotationQueries from "../src/services/queries/rotationQueries";
import { renderWithApp } from "./testUtils";
import type {
  RotationEligiblePerson,
  RotationPlanListItem,
} from "../src/types/rotation";

vi.mock("../src/services/rotationApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/rotationApi")>(
    "../src/services/rotationApi"
  );
  return {
    ...actual,
    createRotationPlan: vi.fn(),
  };
});

vi.mock("../src/services/queries/rotationQueries", async () => {
  const actual = await vi.importActual<typeof import("../src/services/queries/rotationQueries")>(
    "../src/services/queries/rotationQueries"
  );
  return {
    ...actual,
    useRotationEligiblePeople: vi.fn(),
    useRotationPlans: vi.fn(),
  };
});

const mockedCreateRotationPlan = vi.mocked(rotationApi.createRotationPlan);
const mockedUseRotationEligiblePeople = vi.mocked(rotationQueries.useRotationEligiblePeople);
const mockedUseRotationPlans = vi.mocked(rotationQueries.useRotationPlans);

function createRotationEligiblePerson(
  overrides: Partial<RotationEligiblePerson> = {}
): RotationEligiblePerson {
  return {
    personId: 11,
    displayName: "Anika Sattler",
    firstName: "Anika",
    lastName: "Sattler",
    employeeNumber: 4711,
    badgeNumber: 98,
    departmentId: 3,
    departmentName: "BS",
    roleId: 5,
    roleName: "Studentin",
    employmentStatus: "active",
    appUserId: null,
    directoryIdentityId: null,
    directoryLinkStatus: "unlinked",
    directoryDisplayName: null,
    directoryUserPrincipalName: null,
    directoryMail: null,
    directoryEmployeeNumber: null,
    latestSourceWorkflowUid: "wf-onboarding-1",
    latestSourceWorkflowCompletedAt: "2026-06-01T08:00:00.000Z",
    ...overrides,
  };
}

function createRotationPlan(overrides: Partial<RotationPlanListItem> = {}): RotationPlanListItem {
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
    status: "draft",
    createdByUserId: 99,
    stationCount: 2,
    createdAt: "2026-06-05T08:00:00.000Z",
    updatedAt: "2026-06-06T08:00:00.000Z",
    ...overrides,
  };
}

describe("RotationPlanningPage", () => {
  beforeEach(() => {
    mockedCreateRotationPlan.mockReset();
    mockedUseRotationEligiblePeople.mockReset();
    mockedUseRotationPlans.mockReset();

    mockedUseRotationEligiblePeople.mockReturnValue({
      data: [createRotationEligiblePerson()],
      isLoading: false,
      isFetching: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);

    mockedUseRotationPlans.mockReturnValue({
      data: [createRotationPlan()],
      isLoading: false,
      isFetching: false,
      error: null,
      refetch: vi.fn().mockResolvedValue(undefined),
    } as never);
  });

  it("shows eligible people and existing plans for the selected person", async () => {
    renderWithApp(<RotationPlanningPage />, { roleKeys: ["auth_hr"], route: "/rotation?mode=create" });

    expect(await screen.findByRole("heading", { name: "Anika Sattler" })).toBeTruthy();
    expect(screen.getByText("BS")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Person öffnen" }));

    expect(await screen.findByRole("heading", { name: "Personendetail" })).toBeTruthy();
    expect(screen.getAllByTitle("wf-onboarding-1")).toHaveLength(2);
    const planSection = screen.getByRole("heading", { name: "Bestehende Durchlaufpläne" }).closest("section");
    expect(planSection).toBeTruthy();
    expect(
      within(planSection ?? document.body).getByRole("link", { name: "Plan öffnen" }).getAttribute("href")
    ).toBe("/rotation/plans/42");
  });

  it("creates a rotation plan from the selected person's latest completed onboarding", async () => {
    mockedCreateRotationPlan.mockResolvedValue({
      id: 77,
      personId: 11,
      sourceWorkflowUid: "wf-onboarding-1",
      displayName: "Anika Sattler",
      firstName: "Anika",
      lastName: "Sattler",
      departmentId: 3,
      departmentName: "BS",
      title: "HR Durchlauf",
      status: "draft",
      createdByUserId: 99,
      stationCount: 0,
      createdAt: "2026-06-07T08:00:00.000Z",
      updatedAt: "2026-06-07T08:00:00.000Z",
      stations: [],
    });

    renderWithApp(<RotationPlanningPage />, { roleKeys: ["auth_hr"], route: "/rotation?mode=create" });

    fireEvent.click(await screen.findByRole("button", { name: "Person öffnen" }));
    fireEvent.change(screen.getByLabelText("Titel"), { target: { value: "HR Durchlauf" } });
    fireEvent.click(screen.getByRole("button", { name: "Durchlaufplan anlegen" }));

    await waitFor(() => {
      expect(mockedCreateRotationPlan).toHaveBeenCalledWith({
        personId: 11,
        sourceWorkflowUid: "wf-onboarding-1",
        title: "HR Durchlauf",
        status: "draft",
      });
    });
  });
});
