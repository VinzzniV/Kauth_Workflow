import { useEffect, useMemo, useState } from "react";
import AdminOrganizationRelationsPanel from "./AdminOrganizationRelationsPanel";
import { AdminOrganizationDepartmentEditor } from "./AdminOrganizationDepartmentEditor";
import { useAdminOrganizationWorkspaceView } from "./useAdminOrganizationWorkspaceView";
import {
  filterOrganizationDepartments,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../../types/auth";
import type { DepartmentDraft, PositionDraft } from "./adminOrganizationTypes";

type ValidityFilter = "all" | "valid" | "invalid";

type AdminAbteilungenSectionProps = {
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedDepartmentPositions: AdminRole[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  eligibleRequirementOwnerUsers: AdminUser[];
  selectedUser: AdminUser | null;
  newDepartmentNameDraft: string;
  newPositionNameDraft: string;
  departmentDrafts: Record<number, DepartmentDraft>;
  positionDrafts: Record<number, PositionDraft>;
  isCreatingDepartment: boolean;
  creatingPositionDepartmentId: number | null;
  deletingDepartmentId: number | null;
  deletingPositionId: number | null;
  savingDepartmentId: number | null;
  savingPositionId: number | null;
  onSelectOrganizationEntity: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onNewDepartmentNameChange: (value: string) => void;
  onNewPositionNameChange: (value: string) => void;
  onDepartmentDraftChange: (departmentId: number, draft: DepartmentDraft) => void;
  onCreateDepartment: () => void | Promise<void>;
  onCreateDepartmentPosition: (departmentId: number) => void | Promise<void>;
  onSaveDepartmentAssignment: (departmentId: number) => void | Promise<void>;
  onRemoveDepartment: (department: AdminDepartmentAssignment) => void | Promise<void>;
  onPositionDraftChange: (positionId: number, draft: PositionDraft) => void;
  onSaveDepartmentPosition: (positionId: number) => void | Promise<void>;
  onRemoveDepartmentPosition: (position: AdminRole) => void | Promise<void>;
};

type DrawerMode = "edit" | "create" | null;

export function AdminAbteilungenSection(props: AdminAbteilungenSectionProps) {
  const [search, setSearch] = useState("");
  const [leadFilter, setLeadFilter] = useState<ValidityFilter>("all");
  const [ownerFilter, setOwnerFilter] = useState<ValidityFilter>("all");

  const initialMode: DrawerMode = props.selectedEntityId ? "edit" : null;
  const [drawerMode, setDrawerMode] = useState<DrawerMode>(initialMode);

  useEffect(() => {
    if (props.selectedEntityId) {
      setDrawerMode("edit");
    }
  }, [props.selectedEntityId]);

  const filteredDepartments = useMemo(
    () =>
      filterOrganizationDepartments({
        departments: props.sortedDepartments,
        search,
        leadFilter,
        ownerFilter,
        eligibleSupervisorUsers: props.eligibleSupervisorUsers,
        eligibleRequirementOwnerUsers: props.eligibleRequirementOwnerUsers,
      }),
    [
      leadFilter,
      ownerFilter,
      props.eligibleRequirementOwnerUsers,
      props.eligibleSupervisorUsers,
      props.sortedDepartments,
      search,
    ]
  );

  const view = useAdminOrganizationWorkspaceView({
    organizationEntity: "department",
    selectedEntityId: props.selectedEntityId,
    sortedUsers: props.sortedUsers,
    sortedDepartmentPositions: props.sortedDepartmentPositions,
    sortedDepartments: props.sortedDepartments,
    sortedResponsibilities: props.sortedResponsibilities,
    eligibleSupervisorUsers: props.eligibleSupervisorUsers,
    eligibleRequirementOwnerUsers: props.eligibleRequirementOwnerUsers,
    selectedUser: props.selectedUser,
    userDisplayNameDraft: "",
    userEmailDraft: "",
    userNotificationEmailDraft: "",
    userExternalKeyDraft: "",
    userDepartmentIdDraft: "",
    userIsActiveDraft: false,
    newUserDisplayNameDraft: "",
    newUserEmailDraft: "",
    isCreatingUser: false,
    isSavingUserMasterData: false,
    departmentDrafts: props.departmentDrafts,
    responsibilityDrafts: {},
    savingDepartmentId: props.savingDepartmentId,
    savingResponsibilityId: null,
  });

  const closeDrawer = () => {
    setDrawerMode(null);
    if (props.selectedEntityId) {
      props.onSelectOrganizationEntity("department", null);
    }
  };

  const openCreate = () => {
    if (props.selectedEntityId) {
      props.onSelectOrganizationEntity("department", null);
    }
    setDrawerMode("create");
  };

  const openEdit = (department: AdminDepartmentAssignment) => {
    props.onSelectOrganizationEntity("department", department.departmentId);
    setDrawerMode("edit");
  };

  const drawerDepartment = drawerMode === "edit" ? view.selectedDepartment : null;

  const departmentMemberCounts = useMemo(() => {
    const counts = new Map<number, number>();
    for (const user of props.sortedUsers) {
      if (user.departmentId == null) continue;
      counts.set(user.departmentId, (counts.get(user.departmentId) ?? 0) + 1);
    }
    return counts;
  }, [props.sortedUsers]);

  return (
    <div className="content-stack admin-list-page">
      <div className="admin-list-toolbar">
        <label className="field compact grow">
          <span>Suche</span>
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Abteilungsname"
          />
        </label>

        <label className="field compact">
          <span>Leitung</span>
          <select value={leadFilter} onChange={(event) => setLeadFilter(event.target.value as ValidityFilter)}>
            <option value="all">Alle</option>
            <option value="valid">Mit gültiger Leitung</option>
            <option value="invalid">Ohne gültige Leitung</option>
          </select>
        </label>

        <label className="field compact">
          <span>Anforderungsverantwortung</span>
          <select value={ownerFilter} onChange={(event) => setOwnerFilter(event.target.value as ValidityFilter)}>
            <option value="all">Alle</option>
            <option value="valid">Mit Verantwortung</option>
            <option value="invalid">Ohne Verantwortung</option>
          </select>
        </label>

        <button type="button" className="btn btn-primary" onClick={openCreate}>
          Neue Abteilung
        </button>
      </div>

      <p className="panel-note admin-list-summary">
        {filteredDepartments.length} von {props.sortedDepartments.length}{" "}
        {props.sortedDepartments.length === 1 ? "Abteilung" : "Abteilungen"}
      </p>

      <div className="table-scroll">
        <table className="table admin-data-table">
          <thead>
            <tr>
              <th>Abteilung</th>
              <th>Leitung</th>
              <th>Anforderungsverantwortung</th>
              <th>Personen</th>
            </tr>
          </thead>
          <tbody>
            {filteredDepartments.map((department) => {
              const isSelected = props.selectedEntityId === department.departmentId;
              const leadName = department.departmentLeadDisplayName;
              const ownerName = department.requirementOwnerDisplayName;
              const memberCount = departmentMemberCounts.get(department.departmentId) ?? 0;
              return (
                <tr
                  key={department.departmentId}
                  className={`admin-data-row${isSelected ? " admin-data-row--selected" : ""}`}
                  onClick={() => openEdit(department)}
                >
                  <td>
                    <div className="admin-data-cell-strong">{department.departmentName}</div>
                  </td>
                  <td>{leadName ?? <span className="meta-empty">Nicht zugewiesen</span>}</td>
                  <td>{ownerName ?? <span className="meta-empty">Nicht zugewiesen</span>}</td>
                  <td>{memberCount}</td>
                </tr>
              );
            })}
            {filteredDepartments.length === 0 ? (
              <tr>
                <td colSpan={4} className="admin-data-empty">
                  Keine Abteilungen passen zu den Filtern.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {drawerMode ? (
        <>
          <div className="admin-drawer-backdrop" onClick={closeDrawer} aria-hidden="true" />
          <aside
            className="admin-drawer"
            role="dialog"
            aria-modal="true"
            aria-label={drawerMode === "edit" ? "Abteilung bearbeiten" : "Neue Abteilung"}
          >
            <header className="admin-drawer-head">
              <div className="admin-drawer-title">
                <h2>
                  {drawerMode === "edit"
                    ? drawerDepartment?.departmentName ?? "Abteilung bearbeiten"
                    : "Neue Abteilung"}
                </h2>
              </div>
              <button type="button" className="admin-drawer-close" onClick={closeDrawer} aria-label="Schließen">
                ×
              </button>
            </header>

            <div className="admin-drawer-body content-stack">
              <AdminOrganizationDepartmentEditor
                selectedDepartment={drawerDepartment}
                selectedDepartmentDraft={view.selectedDepartmentDraft}
                selectedDepartmentPositions={view.selectedDepartmentPositions}
                positionDrafts={props.positionDrafts}
                selectedDepartmentLeadOptions={view.selectedDepartmentLeadOptions}
                selectedDepartmentOwnerOptions={view.selectedDepartmentOwnerOptions}
                eligibleSupervisorUsers={props.eligibleSupervisorUsers}
                eligibleRequirementOwnerUsers={props.eligibleRequirementOwnerUsers}
                newDepartmentNameDraft={props.newDepartmentNameDraft}
                newPositionNameDraft={props.newPositionNameDraft}
                isCreatingDepartment={props.isCreatingDepartment}
                creatingPositionDepartmentId={props.creatingPositionDepartmentId}
                deletingDepartmentId={props.deletingDepartmentId}
                deletingPositionId={props.deletingPositionId}
                savingDepartmentId={props.savingDepartmentId}
                savingPositionId={props.savingPositionId}
                canSaveDepartment={view.canSaveDepartment}
                onNewDepartmentNameChange={props.onNewDepartmentNameChange}
                onNewPositionNameChange={props.onNewPositionNameChange}
                onDepartmentDraftChange={props.onDepartmentDraftChange}
                onCreateDepartment={props.onCreateDepartment}
                onCreateDepartmentPosition={props.onCreateDepartmentPosition}
                onSaveDepartmentAssignment={props.onSaveDepartmentAssignment}
                onRemoveDepartment={props.onRemoveDepartment}
                onPositionDraftChange={props.onPositionDraftChange}
                onSaveDepartmentPosition={props.onSaveDepartmentPosition}
                onRemoveDepartmentPosition={props.onRemoveDepartmentPosition}
              />

              {drawerMode === "edit" && drawerDepartment ? (
                <AdminOrganizationRelationsPanel
                  organizationEntity="department"
                  selectedUser={null}
                  userRelations={null}
                  selectedDepartment={drawerDepartment}
                  departmentRelations={view.departmentRelations}
                  selectedResponsibility={null}
                  responsibilityRelations={null}
                  onSelectOrganizationEntity={props.onSelectOrganizationEntity}
                />
              ) : null}
            </div>
          </aside>
        </>
      ) : null}
    </div>
  );
}
