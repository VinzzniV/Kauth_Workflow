import { useState } from "react";
import { AdminPermissionsSection } from "./AdminPermissionsSection";
import { AdminTechnicalAccessSection } from "./AdminTechnicalAccessSection";
import type { AdminConfigWorkspaceContentProps } from "./adminConfigWorkspaceContentTypes";

type AccessTab = "person" | "standards";

export function AdminAccessWorkspaceContent({ props }: { props: AdminConfigWorkspaceContentProps }) {
  const { user, access, organization } = props;
  const [activeTab, setActiveTab] = useState<AccessTab>("person");

  return (
    <div className="content-stack">
      <div className="admin-tab-strip" role="tablist" aria-label="App-Rechte-Bereiche">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "person"}
          className={`admin-tab ${activeTab === "person" ? "active" : ""}`}
          onClick={() => setActiveTab("person")}
        >
          Person prüfen
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "standards"}
          className={`admin-tab ${activeTab === "standards" ? "active" : ""}`}
          onClick={() => setActiveTab("standards")}
        >
          Rollen-Standardrechte
        </button>
      </div>

      {activeTab === "person" ? (
        <div className="content-stack">
          <AdminTechnicalAccessSection
            isTechnicalAccessOpen={true}
            isLoadingTechnicalAccess={access.isLoadingTechnicalAccess}
            sortedUsers={user.sortedUsers}
            selectedUser={user.selectedUser}
            selectedUserRoleIds={access.selectedUserRoleIds}
            selectedUserGroupIds={access.selectedUserGroupIds}
            selectedGroupId={access.selectedGroupId}
            selectedGroup={access.selectedGroup}
            selectedGroupRoleIds={access.selectedGroupRoleIds}
            sortedRoles={access.sortedRoles}
            groups={access.groups}
            isSavingUserRoles={access.isSavingUserRoles}
            isSavingUserGroups={access.isSavingUserGroups}
            isSavingGroupRoles={access.isSavingGroupRoles}
            onSelectUser={user.onSelectUser}
            onToggleUserRole={access.onToggleUserRole}
            onToggleUserGroup={access.onToggleUserGroup}
            onSelectGroup={access.onSelectGroup}
            onToggleGroupRole={access.onToggleGroupRole}
            onSaveUserRoles={access.onSaveUserRoles}
            onSaveUserGroups={access.onSaveUserGroups}
            onSaveGroupRoles={access.onSaveGroupRoles}
          />
          <AdminPermissionsSection
            view="exceptions"
            roles={access.sortedRoles}
            permissions={access.permissions}
            auditEntries={access.permissionAuditEntries}
            hasMoreAudit={access.hasMorePermissionAudit}
            isLoadingMoreAudit={access.isLoadingMorePermissionAudit}
            onLoadMoreAudit={access.onLoadMorePermissionAudit}
            departments={organization.sortedDepartments}
            selectedRoleId={access.selectedRoleId}
            selectedRolePermissionIds={access.selectedRolePermissionIds}
            selectedUser={user.selectedUser}
            userOverrideDrafts={access.userOverrideDrafts}
            isLoading={access.isLoadingTechnicalAccess}
            isSavingRolePermissions={access.isSavingRolePermissions}
            isSavingUserOverrides={access.isSavingUserOverrides}
            onSelectRole={access.onSelectRole}
            onToggleRolePermission={access.onToggleRolePermission}
            onSaveRolePermissions={access.onSaveRolePermissions}
            onUserOverrideDraftsChange={access.onUserOverrideDraftsChange}
            onSaveUserOverrides={access.onSaveUserOverrides}
          />
        </div>
      ) : (
        <AdminPermissionsSection
          view="standards"
          roles={access.sortedRoles}
          permissions={access.permissions}
          auditEntries={access.permissionAuditEntries}
          hasMoreAudit={access.hasMorePermissionAudit}
          isLoadingMoreAudit={access.isLoadingMorePermissionAudit}
          onLoadMoreAudit={access.onLoadMorePermissionAudit}
          departments={organization.sortedDepartments}
          selectedRoleId={access.selectedRoleId}
          selectedRolePermissionIds={access.selectedRolePermissionIds}
          selectedUser={user.selectedUser}
          userOverrideDrafts={access.userOverrideDrafts}
          isLoading={access.isLoadingTechnicalAccess}
          isSavingRolePermissions={access.isSavingRolePermissions}
          isSavingUserOverrides={access.isSavingUserOverrides}
          onSelectRole={access.onSelectRole}
          onToggleRolePermission={access.onToggleRolePermission}
          onSaveRolePermissions={access.onSaveRolePermissions}
          onUserOverrideDraftsChange={access.onUserOverrideDraftsChange}
          onSaveUserOverrides={access.onSaveUserOverrides}
        />
      )}
    </div>
  );
}
