import { useCallback, useEffect, useMemo } from "react";
import type { SetURLSearchParams } from "react-router-dom";
import {
  buildAdminOverviewWarnings,
  type AdminOrganizationEntity,
  type AdminWorkspaceSection,
  type AdminWorkspaceWarning,
} from "../components/admin-config/adminWorkspaceModel";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminPermission,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../types/auth";

// Pure derived view data fuer die AdminConfigPage. Ersetzt die alte
// "Mega-Hook"-Variante, die den kompletten flachen Prop-Bag zusammengebaut hat.
//
// Verantwortung:
// - sortierte Listen (Memo)
// - abgeleitete Filter (eligibleSupervisorUsers, ...)
// - Overview-Warnungen
// - URL-Search-Param-Navigation (Section + Organization-Entity)
// - hasAnyData-Flag fuer den Render-Branch der Page
// - Sync zwischen ?id=… in der URL und der internen User-Selection
//
// Bundle-Komposition (user/organization/access/directory/notification/system)
// passiert direkt in der Page, nicht hier.
type UseAdminConfigPageViewArgs = {
  searchParams: URLSearchParams;
  setSearchParams: SetURLSearchParams;
  section: AdminWorkspaceSection;
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  users: AdminUser[];
  departmentPositions: AdminRole[];
  roles: AdminRole[];
  groups: AdminGroup[];
  permissions: AdminPermission[];
  directoryGroups: AdminDirectoryGroup[];
  directoryIdentities: AdminDirectoryIdentity[];
  departmentAssignments: AdminDepartmentAssignment[];
  responsibilityOwners: AdminResponsibilityOwner[];
  notificationEmailConfiguration: AdminNotificationEmailConfiguration | null;
  selectedUser: AdminUser | null;
  selectedUserId: number | null;
  onSelectUser: (user: AdminUser) => void;
};

export type AdminConfigPageView = {
  sortedUsers: AdminUser[];
  sortedDepartmentPositions: AdminRole[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  sortedRoles: AdminRole[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  workspaceSelectedUser: AdminUser | null;
  warnings: AdminWorkspaceWarning[];
  hasAnyData: boolean;
  handleSelectSection: (section: AdminWorkspaceSection) => void;
  handleOpenOrganization: (entity: AdminOrganizationEntity, id?: number | null) => void;
};

export function useAdminConfigPageView(args: UseAdminConfigPageViewArgs): AdminConfigPageView {
  const {
    searchParams,
    setSearchParams,
    section,
    organizationEntity: _organizationEntity,
    selectedEntityId,
    users,
    departmentPositions,
    roles,
    groups: _groups,
    permissions,
    directoryGroups,
    directoryIdentities,
    departmentAssignments,
    responsibilityOwners,
    notificationEmailConfiguration,
    selectedUser,
    selectedUserId,
    onSelectUser,
  } = args;
  void _groups;
  void _organizationEntity;

  const sortedUsers = useMemo(
    () => users.slice().sort((left, right) => left.displayName.localeCompare(right.displayName, "de")),
    [users]
  );
  const sortedDepartmentPositions = useMemo(
    () =>
      departmentPositions.slice().sort((left, right) => {
        const leftKey = `${left.departmentName ?? ""}|${left.roleName}`;
        const rightKey = `${right.departmentName ?? ""}|${right.roleName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [departmentPositions]
  );
  const eligibleSupervisorUsers = useMemo(
    () =>
      sortedUsers.filter(
        (user) => user.isActive && Boolean(user.canAccessSupervisorStep ?? user.hasManagerAccess)
      ),
    [sortedUsers]
  );
  const eligibleRequirementOwnerUsers = useMemo(
    () => sortedUsers.filter((user) => user.isActive),
    [sortedUsers]
  );
  const sortedRoles = useMemo(
    () =>
      roles.slice().sort((left, right) => {
        const leftKey = `${left.roleKind}|${left.departmentName ?? ""}|${left.roleName}`;
        const rightKey = `${right.roleKind}|${right.departmentName ?? ""}|${right.roleName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [roles]
  );
  const sortedDepartments = useMemo(
    () =>
      departmentAssignments
        .slice()
        .sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de")),
    [departmentAssignments]
  );
  const sortedResponsibilities = useMemo(
    () =>
      responsibilityOwners.slice().sort((left, right) => {
        const leftKey = `${left.responsibilityType}|${left.departmentName ?? ""}|${left.responsibilityName}`;
        const rightKey = `${right.responsibilityType}|${right.departmentName ?? ""}|${right.responsibilityName}`;
        return leftKey.localeCompare(rightKey, "de");
      }),
    [responsibilityOwners]
  );

  const workspaceSelectedUser =
    section === "personen" && selectedEntityId ? selectedUser : null;

  const warnings = useMemo(
    () =>
      buildAdminOverviewWarnings({
        departments: sortedDepartments,
        responsibilities: sortedResponsibilities,
        eligibleSupervisorUsers,
        eligibleRequirementOwnerUsers,
        notificationEmailConfiguration,
      }),
    [
      eligibleRequirementOwnerUsers,
      eligibleSupervisorUsers,
      notificationEmailConfiguration,
      sortedDepartments,
      sortedResponsibilities,
    ]
  );

  useEffect(() => {
    if (section !== "personen" || !selectedEntityId) {
      return;
    }

    const matchingUser = users.find((user) => user.userId === selectedEntityId);
    if (!matchingUser || selectedUserId === matchingUser.userId) {
      return;
    }

    onSelectUser(matchingUser);
  }, [onSelectUser, section, selectedEntityId, selectedUserId, users]);

  const updateWorkspace = useCallback(
    (nextSection: AdminWorkspaceSection, nextId?: number | null) => {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.set("section", nextSection);
      nextParams.delete("entity");

      const supportsId = nextSection === "personen" || nextSection === "abteilungen";
      if (supportsId && nextId) {
        nextParams.set("id", String(nextId));
      } else {
        nextParams.delete("id");
      }

      setSearchParams(nextParams);
    },
    [searchParams, setSearchParams]
  );

  const handleSelectSection = useCallback(
    (nextSection: AdminWorkspaceSection) => {
      updateWorkspace(nextSection);
    },
    [updateWorkspace]
  );

  const handleOpenOrganization = useCallback(
    (entity: AdminOrganizationEntity, id?: number | null) => {
      const targetSection: AdminWorkspaceSection = entity === "department" ? "abteilungen" : "personen";
      updateWorkspace(targetSection, id ?? null);
    },
    [updateWorkspace]
  );

  const hasAnyData =
    users.length > 0 ||
    departmentAssignments.length > 0 ||
    responsibilityOwners.length > 0 ||
    permissions.length > 0 ||
    directoryGroups.length > 0 ||
    directoryIdentities.length > 0;

  return {
    sortedUsers,
    sortedDepartmentPositions,
    sortedDepartments,
    sortedResponsibilities,
    sortedRoles,
    eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers,
    workspaceSelectedUser,
    warnings,
    hasAnyData,
    handleSelectSection,
    handleOpenOrganization,
  };
}
