import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AdminOrganizationResponsibilityEditor } from "../src/components/admin-config/AdminOrganizationResponsibilityEditor";
import { createAdminDepartmentAssignment, createAdminUser } from "./testUtils";

describe("AdminOrganizationResponsibilityEditor", () => {
  it("allows creating responsibilities with a plain name and separate department", () => {
    const onNewResponsibilityDraftChange = vi.fn();
    const onCreateResponsibility = vi.fn();

    render(
      <AdminOrganizationResponsibilityEditor
        newResponsibilityDraft={{ responsibilityName: "AD", departmentId: "1" }}
        selectedResponsibility={null}
        selectedResponsibilityDraft={null}
        sortedDepartments={[createAdminDepartmentAssignment()]}
        sortedUsers={[createAdminUser()]}
        isCreatingResponsibility={false}
        deletingResponsibilityId={null}
        savingResponsibilityId={null}
        canSaveResponsibility={false}
        onNewResponsibilityDraftChange={onNewResponsibilityDraftChange}
        onCreateResponsibility={onCreateResponsibility}
        onResponsibilityDraftChange={vi.fn()}
        onRemoveResponsibility={vi.fn()}
        onSaveResponsibilityAssignment={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText("Name der Zuständigkeit"), {
      target: { value: "Mailbox" },
    });

    expect(onNewResponsibilityDraftChange).toHaveBeenCalledWith({
      responsibilityName: "Mailbox",
      departmentId: "1",
    });

    fireEvent.click(screen.getByRole("button", { name: "Zuständigkeit anlegen" }));
    expect(onCreateResponsibility).toHaveBeenCalled();
  });

  it("shows a delete action for the selected responsibility", () => {
    const onRemoveResponsibility = vi.fn();

    render(
      <AdminOrganizationResponsibilityEditor
        newResponsibilityDraft={{ responsibilityName: "", departmentId: "" }}
        selectedResponsibility={{
          responsibilityId: 5,
          responsibilityKey: "admin_mailbox_1234abcd",
          systemKey: "system_mailbox_1234abcd",
          responsibilityName: "Mailbox",
          responsibilityType: "application",
          departmentId: 1,
          departmentName: "IT",
          appUserId: 1,
          appUserDisplayName: "Lea Lead",
          updatedAt: "2026-04-09T10:00:00.000Z",
        }}
        selectedResponsibilityDraft={{ appUserId: "1", departmentId: "1" }}
        sortedDepartments={[createAdminDepartmentAssignment()]}
        sortedUsers={[createAdminUser()]}
        isCreatingResponsibility={false}
        deletingResponsibilityId={null}
        savingResponsibilityId={null}
        canSaveResponsibility={false}
        onNewResponsibilityDraftChange={vi.fn()}
        onCreateResponsibility={vi.fn()}
        onResponsibilityDraftChange={vi.fn()}
        onRemoveResponsibility={onRemoveResponsibility}
        onSaveResponsibilityAssignment={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Zuständigkeit löschen" }));
    expect(onRemoveResponsibility).toHaveBeenCalledWith(
      expect.objectContaining({ responsibilityId: 5, responsibilityName: "Mailbox" })
    );
  });
});
