import { fireEvent, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { AdminDepartmentEntraImportSection } from "../src/components/admin-config/AdminDepartmentEntraImportSection";
import type { AdminRole } from "../src/types/auth";
import { renderWithApp } from "./testUtils";
import {
  getDepartmentEntraJobTitles,
  importDepartmentPositionsFromEntra,
} from "../src/services/adminApi";

vi.mock("../src/services/adminApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/adminApi")>("../src/services/adminApi");
  return {
    ...actual,
    getDepartmentEntraJobTitles: vi.fn(),
    importDepartmentPositionsFromEntra: vi.fn(),
  };
});

const mockedGetDepartmentEntraJobTitles = vi.mocked(getDepartmentEntraJobTitles);
const mockedImportDepartmentPositionsFromEntra = vi.mocked(importDepartmentPositionsFromEntra);

function createRole(overrides: Partial<AdminRole> = {}): AdminRole {
  return {
    roleId: 1,
    roleKey: "worker",
    roleName: "Mitarbeiter",
    roleKind: "department",
    departmentId: 7,
    departmentName: "Ausbildung technisch",
    scope: "global",
    scopeDepartmentId: null,
    scopeDepartmentName: null,
    isActive: true,
    permissions: [],
    ...overrides,
  };
}

describe("AdminDepartmentEntraImportSection", () => {
  beforeEach(() => {
    mockedGetDepartmentEntraJobTitles.mockResolvedValue(["Ausbildungsleitung", "Mitarbeiter"]);
    mockedImportDepartmentPositionsFromEntra.mockResolvedValue({ created: 1, skipped: 0 });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("refreshes organization data after a successful Entra position import", async () => {
    const onRefreshData = vi.fn().mockResolvedValue(undefined);

    renderWithApp(
      <AdminDepartmentEntraImportSection
        departmentId={7}
        existingPositions={[createRole()]}
        onRefreshData={onRefreshData}
      />
    );

    fireEvent.click(await screen.findByRole("button", { name: /Ausbildungsleitung/i }));
    fireEvent.click(screen.getByRole("button", { name: /1 Stelle importieren/i }));

    await waitFor(() => {
      expect(mockedImportDepartmentPositionsFromEntra).toHaveBeenCalledWith(7, ["Ausbildungsleitung"]);
    });

    await waitFor(() => {
      expect(onRefreshData).toHaveBeenCalledTimes(1);
    });

    expect(await screen.findByText(/1 Stelle importiert/i)).toBeTruthy();
  });

  it("renders already existing titles as locked reference cards", async () => {
    renderWithApp(
      <AdminDepartmentEntraImportSection
        departmentId={7}
        existingPositions={[createRole()]}
      />
    );

    const existingTitleButton = await screen.findByRole("button", { name: /Mitarbeiter/i });
    expect((existingTitleButton as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText(/1 bereits vorhanden/i)).toBeTruthy();
  });
});
