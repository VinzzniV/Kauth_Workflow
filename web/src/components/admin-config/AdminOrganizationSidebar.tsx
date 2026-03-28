import { useMemo, useState } from "react";
import type {
  AdminDepartmentAssignment,
  AdminResponsibilityOwner,
  AdminUser,
} from "../../types/auth";
import {
  filterOrganizationDepartments,
  filterOrganizationResponsibilities,
  filterOrganizationUsers,
  type AdminOrganizationEntity,
} from "./adminWorkspaceModel";
import {
  responsibilityAreaLabel,
  responsibilityTypeLabel,
} from "./adminConfigHelpers";
import { ADMIN_ORGANIZATION_ENTITY_LABELS } from "./adminOrganizationTypes";
import SectionHeader from "../ui/SectionHeader";
import SelectionListItem from "../ui/SelectionListItem";

type UserActivityFilter = "all" | "active" | "inactive";
type ValidityFilter = "all" | "valid" | "invalid";
type ResponsibilityTypeFilter = "all" | "process" | "application";
type ResponsibilityPresenceFilter = "all" | "with_person" | "without_person";
type ResponsibilityDepartmentFilter = "all" | "with_department" | "without_department";

type AdminOrganizationSidebarProps = {
  organizationEntity: AdminOrganizationEntity;
  selectedEntityId: number | null;
  sortedUsers: AdminUser[];
  sortedDepartments: AdminDepartmentAssignment[];
  sortedResponsibilities: AdminResponsibilityOwner[];
  eligibleSupervisorUsers: AdminUser[];
  onSelectOrganizationEntity: (entity: AdminOrganizationEntity, id?: number | null) => void;
  onSelectUser: (user: AdminUser) => void;
};

export function AdminOrganizationSidebar({
  organizationEntity,
  selectedEntityId,
  sortedUsers,
  sortedDepartments,
  sortedResponsibilities,
  eligibleSupervisorUsers,
  onSelectOrganizationEntity,
  onSelectUser,
}: AdminOrganizationSidebarProps) {
  const [organizationSearch, setOrganizationSearch] = useState<string>("");
  const [userActivityFilter, setUserActivityFilter] = useState<UserActivityFilter>("all");
  const [userDepartmentFilter, setUserDepartmentFilter] = useState<string>("");
  const [departmentLeadFilter, setDepartmentLeadFilter] = useState<ValidityFilter>("all");
  const [departmentOwnerFilter, setDepartmentOwnerFilter] = useState<ValidityFilter>("all");
  const [responsibilityTypeFilter, setResponsibilityTypeFilter] =
    useState<ResponsibilityTypeFilter>("all");
  const [responsibilityPersonFilter, setResponsibilityPersonFilter] =
    useState<ResponsibilityPresenceFilter>("all");
  const [responsibilityDepartmentFilter, setResponsibilityDepartmentFilter] =
    useState<ResponsibilityDepartmentFilter>("all");

  const filteredUsers = useMemo(
    () =>
      filterOrganizationUsers({
        users: sortedUsers,
        search: organizationSearch,
        activityFilter: userActivityFilter,
        departmentFilter: userDepartmentFilter,
      }),
    [organizationSearch, sortedUsers, userActivityFilter, userDepartmentFilter]
  );
  const filteredDepartments = useMemo(
    () =>
      filterOrganizationDepartments({
        departments: sortedDepartments,
        search: organizationSearch,
        leadFilter: departmentLeadFilter,
        ownerFilter: departmentOwnerFilter,
        eligibleSupervisorUsers,
      }),
    [
      departmentLeadFilter,
      departmentOwnerFilter,
      eligibleSupervisorUsers,
      organizationSearch,
      sortedDepartments,
    ]
  );
  const filteredResponsibilities = useMemo(
    () =>
      filterOrganizationResponsibilities({
        responsibilities: sortedResponsibilities,
        search: organizationSearch,
        typeFilter: responsibilityTypeFilter,
        personFilter: responsibilityPersonFilter,
        departmentFilter: responsibilityDepartmentFilter,
      }),
    [
      organizationSearch,
      responsibilityDepartmentFilter,
      responsibilityPersonFilter,
      responsibilityTypeFilter,
      sortedResponsibilities,
    ]
  );

  function renderFilters() {
    if (organizationEntity === "user") {
      return (
        <>
          <label className="field">
            <span>Suche</span>
            <input
              type="search"
              value={organizationSearch}
              onChange={(event) => setOrganizationSearch(event.target.value)}
              placeholder="Name, Mail oder Abteilung"
            />
          </label>

          <label className="field">
            <span>Status</span>
            <select
              value={userActivityFilter}
              onChange={(event) => setUserActivityFilter(event.target.value as UserActivityFilter)}
            >
              <option value="all">Alle</option>
              <option value="active">Nur aktiv</option>
              <option value="inactive">Nur inaktiv</option>
            </select>
          </label>

          <label className="field">
            <span>Abteilung</span>
            <select
              value={userDepartmentFilter}
              onChange={(event) => setUserDepartmentFilter(event.target.value)}
            >
              <option value="">Alle Abteilungen</option>
              {sortedDepartments.map((department) => (
                <option key={`user-filter-department-${department.departmentId}`} value={department.departmentId}>
                  {department.departmentName}
                </option>
              ))}
            </select>
          </label>
        </>
      );
    }

    if (organizationEntity === "department") {
      return (
        <>
          <label className="field">
            <span>Suche</span>
            <input
              type="search"
              value={organizationSearch}
              onChange={(event) => setOrganizationSearch(event.target.value)}
              placeholder="Abteilungsname"
            />
          </label>

          <label className="field">
            <span>Leitung</span>
            <select
              value={departmentLeadFilter}
              onChange={(event) => setDepartmentLeadFilter(event.target.value as ValidityFilter)}
            >
              <option value="all">Alle</option>
              <option value="valid">Mit gültiger Leitung</option>
              <option value="invalid">Ohne gültige Leitung</option>
            </select>
          </label>

          <label className="field">
            <span>Anforderungsverantwortung</span>
            <select
              value={departmentOwnerFilter}
              onChange={(event) => setDepartmentOwnerFilter(event.target.value as ValidityFilter)}
            >
              <option value="all">Alle</option>
              <option value="valid">Mit gültiger Person</option>
              <option value="invalid">Ohne gültige Person</option>
            </select>
          </label>
        </>
      );
    }

    return (
      <>
        <label className="field">
          <span>Suche</span>
          <input
            type="search"
            value={organizationSearch}
            onChange={(event) => setOrganizationSearch(event.target.value)}
            placeholder="Name, Bereich oder System-Key"
          />
        </label>

        <label className="field">
          <span>Typ</span>
          <select
            value={responsibilityTypeFilter}
            onChange={(event) =>
              setResponsibilityTypeFilter(event.target.value as ResponsibilityTypeFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="process">Nur Prozess</option>
            <option value="application">Nur System</option>
          </select>
        </label>

        <label className="field">
          <span>Feste Person</span>
          <select
            value={responsibilityPersonFilter}
            onChange={(event) =>
              setResponsibilityPersonFilter(event.target.value as ResponsibilityPresenceFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="with_person">Mit fester Person</option>
            <option value="without_person">Ohne feste Person</option>
          </select>
        </label>

        <label className="field">
          <span>Bereich</span>
          <select
            value={responsibilityDepartmentFilter}
            onChange={(event) =>
              setResponsibilityDepartmentFilter(event.target.value as ResponsibilityDepartmentFilter)
            }
          >
            <option value="all">Alle</option>
            <option value="with_department">Mit Bereich</option>
            <option value="without_department">Ohne Bereich</option>
          </select>
        </label>
      </>
    );
  }

  function renderList() {
    if (organizationEntity === "user") {
      if (filteredUsers.length === 0) {
        return <p className="panel-note">Keine Personen für den aktuellen Filter.</p>;
      }

      return (
        <div className="selection-list" aria-label="Personenliste">
          {filteredUsers.map((user) => (
            <SelectionListItem
              key={user.userId}
              active={selectedEntityId === user.userId}
              title={user.displayName}
              meta={user.email}
              secondaryMeta={`${user.departmentName ?? "Keine Abteilung"} | ${user.isActive ? "Aktiv" : "Inaktiv"}`}
              onClick={() => {
                onSelectUser(user);
                onSelectOrganizationEntity("user", user.userId);
              }}
            />
          ))}
        </div>
      );
    }

    if (organizationEntity === "department") {
      if (filteredDepartments.length === 0) {
        return <p className="panel-note">Keine Abteilungen für den aktuellen Filter.</p>;
      }

      return (
        <div className="selection-list" aria-label="Abteilungsliste">
          {filteredDepartments.map((department) => (
            <SelectionListItem
              key={department.departmentId}
              active={selectedEntityId === department.departmentId}
              title={department.departmentName}
              meta={`Leitung: ${department.departmentLeadDisplayName ?? "nicht festgelegt"}`}
              secondaryMeta={`Anforderung: ${department.requirementOwnerDisplayName ?? "nicht festgelegt"}`}
              onClick={() => onSelectOrganizationEntity("department", department.departmentId)}
            />
          ))}
        </div>
      );
    }

    if (filteredResponsibilities.length === 0) {
      return <p className="panel-note">Keine Zuständigkeiten für den aktuellen Filter.</p>;
    }

    return (
      <div className="selection-list" aria-label="Zuständigkeitsliste">
        {filteredResponsibilities.map((responsibility) => (
          <SelectionListItem
            key={responsibility.responsibilityId}
            active={selectedEntityId === responsibility.responsibilityId}
            title={responsibility.responsibilityName}
            meta={`${responsibilityAreaLabel(responsibility)} | ${responsibilityTypeLabel(responsibility)}`}
            secondaryMeta={`Person: ${responsibility.appUserDisplayName ?? "keine feste Person"}`}
            onClick={() => onSelectOrganizationEntity("responsibility", responsibility.responsibilityId)}
          />
        ))}
      </div>
    );
  }

  return (
    <section className="panel admin-organization-sidebar master-detail-sidebar">
      <SectionHeader
        title={ADMIN_ORGANIZATION_ENTITY_LABELS[organizationEntity]}
        description="Ein primäres Objekt bleibt im Fokus, die Liste links bleibt stabil."
      />

      <div className="admin-entity-switcher" role="group" aria-label="Organisationsobjekte">
        {(["user", "department", "responsibility"] as const).map((entity) => (
          <button
            key={entity}
            type="button"
            className={`admin-entity-switch ${organizationEntity === entity ? "active" : ""}`}
            aria-pressed={organizationEntity === entity}
            onClick={() => onSelectOrganizationEntity(entity, null)}
          >
            {ADMIN_ORGANIZATION_ENTITY_LABELS[entity]}
          </button>
        ))}
      </div>

      {renderFilters()}
      {renderList()}
    </section>
  );
}
