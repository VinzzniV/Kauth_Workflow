import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AdminDepartmentsSection } from "../src/components/admin-config/AdminDepartmentsSection";
import type { AdminDepartmentAssignment, AdminUser } from "../src/types/auth";

function createDepartment(overrides: Partial<AdminDepartmentAssignment> = {}): AdminDepartmentAssignment {
  return {
    departmentId: 1,
    departmentName: "IT",
    departmentLeadUserId: 10,
    departmentLeadDisplayName: "Lea Lead",
    requirementOwnerUserId: 11,
    requirementOwnerDisplayName: "Mia Manager",
    updatedAt: "2026-03-24T08:00:00.000Z",
    ...overrides,
  };
}

function createUser(overrides: Partial<AdminUser> = {}): AdminUser {
  return {
    userId: 10,
    externalKey: "lea.lead",
    displayName: "Lea Lead",
    email: "lea.lead@demo.local",
    notificationEmail: null,
    isActive: true,
    hasManagerAccess: true,
    departmentId: 1,
    departmentName: "IT",
    roles: [],
    groups: [],
    ...overrides,
  };
}

describe("AdminDepartmentsSection", () => {
  it("disables create and save actions until input is valid or changed", () => {
    const onDepartmentDraftChange = vi.fn();

    render(
      <AdminDepartmentsSection
        sortedDepartments={[createDepartment()]}
        sortedUsers={[createUser(), createUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" })]}
        eligibleSupervisorUsers={[createUser(), createUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" })]}
        departmentDrafts={{
          1: {
            departmentLeadUserId: "10",
            requirementOwnerUserId: "11",
          },
        }}
        newDepartmentNameDraft=""
        isCreatingDepartment={false}
        savingDepartmentId={null}
        deletingDepartmentId={null}
        onNewDepartmentNameChange={vi.fn()}
        onDepartmentDraftChange={onDepartmentDraftChange}
        onCreateDepartment={vi.fn()}
        onSaveDepartmentAssignment={vi.fn()}
        onRemoveDepartment={vi.fn()}
      />
    );

    expect((screen.getByRole("button", { name: "Abteilung anlegen" }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: "Zuständigkeit speichern" }) as HTMLButtonElement).disabled).toBe(
      true
    );
    expect(screen.getByText("Keine ungespeicherten Änderungen.")).toBeTruthy();
  });

  it("shows invalid saved assignments and enables save only after a valid change", () => {
    const onDepartmentDraftChange = vi.fn();

    render(
      <AdminDepartmentsSection
        sortedDepartments={[
          createDepartment({
            departmentLeadUserId: 99,
            departmentLeadDisplayName: "Alte Leitung",
          }),
        ]}
        sortedUsers={[
          createUser({ userId: 20, displayName: "Neue Leitung", externalKey: "neue.leitung" }),
          createUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
          createUser({
            userId: 99,
            displayName: "Alte Leitung",
            externalKey: "alte.leitung",
            isActive: false,
            hasManagerAccess: false,
          }),
        ]}
        eligibleSupervisorUsers={[
          createUser({ userId: 20, displayName: "Neue Leitung", externalKey: "neue.leitung" }),
          createUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
        ]}
        departmentDrafts={{
          1: {
            departmentLeadUserId: "99",
            requirementOwnerUserId: "11",
          },
        }}
        newDepartmentNameDraft="Einkauf"
        isCreatingDepartment={false}
        savingDepartmentId={null}
        deletingDepartmentId={null}
        onNewDepartmentNameChange={vi.fn()}
        onDepartmentDraftChange={onDepartmentDraftChange}
        onCreateDepartment={vi.fn()}
        onSaveDepartmentAssignment={vi.fn()}
        onRemoveDepartment={vi.fn()}
      />
    );

    expect(screen.getByText(/Ungültige Zuordnung:/)).toBeTruthy();
    expect((screen.getByRole("button", { name: "Zuständigkeit speichern" }) as HTMLButtonElement).disabled).toBe(
      true
    );

    fireEvent.change(screen.getByLabelText("Abteilungsleitung"), { target: { value: "20" } });

    expect(onDepartmentDraftChange).toHaveBeenCalledWith(1, {
      departmentLeadUserId: "20",
      requirementOwnerUserId: "11",
    });
  });
});
