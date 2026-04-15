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
        eligibleRequirementOwnerUsers={[
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
            canAccessSupervisorStep: false,
          }),
        ]}
        eligibleSupervisorUsers={[
          createAdminUser({ userId: 20, displayName: "Neue Leitung", externalKey: "neue.leitung" }),
          createAdminUser({ userId: 11, displayName: "Mia Manager", externalKey: "mia.manager" }),
        ]}
        eligibleRequirementOwnerUsers={[
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
    expect(screen.getAllByText(/Supervisor-Berechtigung/)).toHaveLength(2);
    expect((screen.getByRole("button", { name: "Zuständigkeit speichern" }) as HTMLButtonElement).disabled).toBe(
      true
    );

    fireEvent.change(screen.getByLabelText("Abteilungsleitung"), { target: { value: "20" } });

    expect(onDepartmentDraftChange).toHaveBeenCalledWith(1, {
      departmentLeadUserId: "20",
      requirementOwnerUserId: "11",
    });
  });

  it("allows active non-supervisor users as requirement owners", () => {
    render(
      <AdminDepartmentsSection
        sortedDepartments={[
          createAdminDepartmentAssignment({
            departmentLeadUserId: 10,
            departmentLeadDisplayName: "Lea Lead",
            requirementOwnerUserId: null,
            requirementOwnerDisplayName: null,
          }),
        ]}
        sortedUsers={[
          createAdminUser({ userId: 10, displayName: "Lea Lead", canAccessSupervisorStep: true }),
          createAdminUser({
            userId: 12,
            displayName: "Nora Normal",
            hasManagerAccess: false,
            canAccessSupervisorStep: false,
          }),
        ]}
        eligibleSupervisorUsers={[
          createAdminUser({ userId: 10, displayName: "Lea Lead", canAccessSupervisorStep: true }),
        ]}
        eligibleRequirementOwnerUsers={[
          createAdminUser({ userId: 10, displayName: "Lea Lead", canAccessSupervisorStep: true }),
          createAdminUser({
            userId: 12,
            displayName: "Nora Normal",
            hasManagerAccess: false,
            canAccessSupervisorStep: false,
          }),
        ]}
        departmentDrafts={{
          1: {
            departmentLeadUserId: "10",
            requirementOwnerUserId: "12",
          },
        }}
        newDepartmentNameDraft=""
        isCreatingDepartment={false}
        savingDepartmentId={null}
        deletingDepartmentId={null}
        onNewDepartmentNameChange={vi.fn()}
        onDepartmentDraftChange={vi.fn()}
        onCreateDepartment={vi.fn()}
        onSaveDepartmentAssignment={vi.fn()}
        onRemoveDepartment={vi.fn()}
      />
    );

    expect(screen.getByRole("option", { name: /Nora Normal/ })).toBeTruthy();
    expect(screen.queryByText(/Ungültige Zuordnung:/)).toBeNull();
    expect((screen.getByRole("button", { name: "Zuständigkeit speichern" }) as HTMLButtonElement).disabled).toBe(
      false
    );
  });

  it("shows Entra sync source and conflict details for managed departments", () => {
    render(
      <AdminDepartmentsSection
        sortedDepartments={[
          createAdminDepartmentAssignment({
            assignmentSource: "entra_managed",
            syncState: "conflict",
            syncDetail: "Mehrere aktive Entra-Abteilungsleitungen gefunden: Lea Lead, Max Manager.",
          }),
        ]}
        sortedUsers={[createAdminUser({ userId: 10 })]}
        eligibleSupervisorUsers={[createAdminUser({ userId: 10 })]}
        eligibleRequirementOwnerUsers={[createAdminUser({ userId: 10 })]}
        departmentDrafts={{
          1: {
            departmentLeadUserId: "1",
            requirementOwnerUserId: "2",
          },
        }}
        newDepartmentNameDraft=""
        isCreatingDepartment={false}
        savingDepartmentId={null}
        deletingDepartmentId={null}
        onNewDepartmentNameChange={vi.fn()}
        onDepartmentDraftChange={vi.fn()}
        onCreateDepartment={vi.fn()}
        onSaveDepartmentAssignment={vi.fn()}
        onRemoveDepartment={vi.fn()}
      />
    );

    expect(screen.getByText(/Quelle: Entra-geführt/)).toBeTruthy();
    expect(screen.getByText(/Status: Entra-Konflikt/)).toBeTruthy();
    expect(screen.getByText(/Mehrere aktive Entra-Abteilungsleitungen gefunden/)).toBeTruthy();
    expect(screen.getByText(/Manuelle Änderungen werden beim nächsten Directory-Sync überschrieben/)).toBeTruthy();
  });
});
