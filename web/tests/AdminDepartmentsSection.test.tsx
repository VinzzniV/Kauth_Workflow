import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AdminDepartmentsSection } from "../src/components/admin-config/AdminDepartmentsSection";
import { createAdminDepartmentAssignment, createAdminUser } from "./testUtils";

describe("AdminDepartmentsSection", () => {
  it("disables create and save actions until input is valid or changed", () => {
    const onDepartmentDraftChange = vi.fn();

    render(
      <AdminDepartmentsSection
        sortedDepartments={[
          createAdminDepartmentAssignment({
            departmentLeadUserId: 10,
            departmentLeadDisplayName: "Lea Lead",
            requirementOwnerUserId: 11,
            requirementOwnerDisplayName: "Mia Manager",
          }),
        ]}
        sortedUsers={[
          createAdminUser({ userId: 10 }),
          createAdminUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
        ]}
        eligibleSupervisorUsers={[
          createAdminUser({ userId: 10 }),
          createAdminUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
        ]}
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
          createAdminDepartmentAssignment({
            departmentLeadUserId: 99,
            departmentLeadDisplayName: "Alte Leitung",
            requirementOwnerUserId: 11,
            requirementOwnerDisplayName: "Mia Manager",
          }),
        ]}
        sortedUsers={[
          createAdminUser({ userId: 20, displayName: "Neue Leitung", externalKey: "neue.leitung" }),
          createAdminUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
          createAdminUser({
            userId: 99,
            displayName: "Alte Leitung",
            externalKey: "alte.leitung",
            isActive: false,
            hasManagerAccess: false,
          }),
        ]}
        eligibleSupervisorUsers={[
          createAdminUser({ userId: 20, displayName: "Neue Leitung", externalKey: "neue.leitung" }),
          createAdminUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
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
