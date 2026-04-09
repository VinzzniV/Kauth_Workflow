import { fireEvent, screen, waitFor } from "@testing-library/react";
import { useCallback, useState } from "react";
import { describe, expect, it } from "vitest";
import { useAdminUserManagement } from "../src/hooks/useAdminUserManagement";
import type { AdminGroup, AdminRole, AdminUser } from "../src/types/auth";
import { renderWithApp } from "./testUtils";

function createRole(roleId: number, roleName: string): AdminRole {
  return {
    roleId,
    roleKey: `role_${roleId}`,
    roleName,
    roleKind: "system",
    departmentId: null,
    departmentName: null,
    scope: "global",
    scopeDepartmentId: null,
    scopeDepartmentName: null,
    isActive: true,
    permissions: [],
  };
}

function createGroup(groupId: number): AdminGroup {
  return {
    groupId,
    groupKey: `group_${groupId}`,
    groupName: `Gruppe ${groupId}`,
    description: null,
    isActive: true,
    roles: [],
  };
}

function createUser(
  userId: number,
  displayName: string,
  roleIds: number[],
  groupIds: number[]
): AdminUser {
  return {
    userId,
    externalKey: `user.${userId}`,
    displayName,
    email: `user.${userId}@demo.local`,
    notificationEmail: null,
    isActive: true,
    hasManagerAccess: true,
    canAccessSupervisorStep: true,
    departmentId: 1,
    departmentName: "IT",
    directorySynced: false,
    departmentSource: "manual",
    departmentOverrideActive: false,
    directoryIdentityId: null,
    userPrincipalName: null,
    directoryDisplayName: null,
    roles: roleIds.map((roleId) => createRole(roleId, `Rolle ${roleId}`)),
    groups: groupIds.map((groupId) => ({
      groupId,
      groupKey: `group_${groupId}`,
      groupName: `Gruppe ${groupId}`,
      description: null,
      isActive: true,
    })),
    effectiveRoles: [],
    permissionOverrides: [],
    effectivePermissions: [],
  };
}

function UserManagementHarness({
  initialUsers,
  refreshedUsers,
}: {
  initialUsers: AdminUser[];
  refreshedUsers: AdminUser[];
}) {
  const [users, setUsers] = useState(initialUsers);
  const [groups, setGroups] = useState<AdminGroup[]>([createGroup(10), createGroup(11)]);
  const onNotice = useCallback(() => undefined, []);
  const onError = useCallback(() => undefined, []);
  const refreshCurrentUser = useCallback(async () => undefined, []);
  const reload = useCallback(async () => undefined, []);

  const userManagement = useAdminUserManagement({
    users,
    groups,
    setUsers,
    setGroups,
    refreshCurrentUser,
    reload,
    onNotice,
    onError,
  });

  return (
    <div>
      <button type="button" onClick={() => userManagement.selectUser(users[0]!)}>
        Person wählen
      </button>
      <button type="button" onClick={() => setUsers(refreshedUsers)}>
        Server-Refresh
      </button>
      <output data-testid="display-name">{userManagement.userDisplayNameDraft}</output>
      <output data-testid="role-ids">{userManagement.selectedUserRoleIds.join(",")}</output>
      <output data-testid="group-ids">{userManagement.selectedUserGroupIds.join(",")}</output>
    </div>
  );
}

describe("useAdminUserManagement", () => {
  it("resyncs the selected user draft when the server payload changes", async () => {
    const initialUsers = [createUser(1, "Lea Lead", [100], [10])];
    const refreshedUsers = [createUser(1, "Lea Lead Aktualisiert", [200, 300], [11])];

    renderWithApp(
      <UserManagementHarness initialUsers={initialUsers} refreshedUsers={refreshedUsers} />
    );

    fireEvent.click(screen.getByRole("button", { name: "Person wählen" }));

    expect(screen.getByTestId("display-name").textContent).toBe("Lea Lead");
    expect(screen.getByTestId("role-ids").textContent).toBe("100");
    expect(screen.getByTestId("group-ids").textContent).toBe("10");

    fireEvent.click(screen.getByRole("button", { name: "Server-Refresh" }));

    await waitFor(() => {
      expect(screen.getByTestId("display-name").textContent).toBe("Lea Lead Aktualisiert");
    });

    expect(screen.getByTestId("role-ids").textContent).toBe("200,300");
    expect(screen.getByTestId("group-ids").textContent).toBe("11");
  });
});
