import { useCallback, useEffect, useRef, useState } from "react";
import type { AdminWorkspaceSection } from "../components/admin-config/adminWorkspaceModel";
import {
  createAdminDirectoryGroupRoleMapping,
  deleteAdminDirectoryGroupRoleMapping,
  getAdminDirectoryAudit,
  getAdminDirectoryGroups,
  getAdminDirectoryIdentities,
  getAdminDirectoryPendingImports,
  getAdminDirectoryResponsibilityGaps,
  getAdminDirectoryStatus,
  postAdminDirectoryImport,
  syncAdminDirectory,
} from "../services/adminConfigApi";
import {
  getAdminDepartmentPositions,
  getAdminDepartmentAssignments,
  getAdminGraphApplicationConfiguration,
  getAdminGroups,
  getAdminNotificationEmailConfiguration,
  getAdminPermissionAudit,
  getAdminPermissions,
  getAdminResponsibilityOwners,
  getAdminRoles,
  getAdminUsers,
} from "../services/adminApi";
import type {
  AdminDepartmentAssignment,
  AdminDirectoryGroup,
  AdminDirectoryIdentity,
  AdminDirectoryMappingAuditEntry,
  AdminDirectorySyncStatus,
  DirectoryPendingImports,
  DirectoryResponsibilityGaps,
  AdminGraphApplicationConfiguration,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminPermission,
  AdminPermissionAuditEntry,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../types/auth";

type UseAdminConfigDataOptions = {
  section: AdminWorkspaceSection;
  setError: (value: string | null) => void;
  setNotice: (value: string | null) => void;
  setGraphApplicationConfiguration: (value: AdminGraphApplicationConfiguration | null) => void;
  setNotificationEmailConfiguration: (value: AdminNotificationEmailConfiguration | null) => void;
};

export function useAdminConfigData({
  section,
  setError,
  setNotice,
  setGraphApplicationConfiguration,
  setNotificationEmailConfiguration,
}: UseAdminConfigDataOptions) {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [groups, setGroups] = useState<AdminGroup[]>([]);
  const [permissions, setPermissions] = useState<AdminPermission[]>([]);
  const [permissionAuditEntries, setPermissionAuditEntries] = useState<AdminPermissionAuditEntry[]>([]);
  const [permissionAuditNextCursor, setPermissionAuditNextCursor] = useState<string | null>(null);
  const [hasMorePermissionAudit, setHasMorePermissionAudit] = useState(false);
  const [isLoadingMorePermissionAudit, setIsLoadingMorePermissionAudit] = useState(false);
  const [directoryGroups, setDirectoryGroups] = useState<AdminDirectoryGroup[]>([]);
  const [directoryIdentities, setDirectoryIdentities] = useState<AdminDirectoryIdentity[]>([]);
  const [directoryAuditEntries, setDirectoryAuditEntries] = useState<AdminDirectoryMappingAuditEntry[]>([]);
  const [directoryAuditNextCursor, setDirectoryAuditNextCursor] = useState<string | null>(null);
  const [hasMoreDirectoryAudit, setHasMoreDirectoryAudit] = useState(false);
  const [isLoadingMoreDirectoryAudit, setIsLoadingMoreDirectoryAudit] = useState(false);
  const [directoryStatus, setDirectoryStatus] = useState<AdminDirectorySyncStatus | null>(null);
  const [directoryResponsibilityGaps, setDirectoryResponsibilityGaps] = useState<DirectoryResponsibilityGaps | null>(null);
  const [directoryPendingImports, setDirectoryPendingImports] = useState<DirectoryPendingImports | null>(null);
  const [departmentAssignments, setDepartmentAssignments] = useState<AdminDepartmentAssignment[]>([]);
  const [departmentPositions, setDepartmentPositions] = useState<AdminRole[]>([]);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingTechnicalAccess, setIsLoadingTechnicalAccess] = useState(false);
  const [isLoadingDirectory, setIsLoadingDirectory] = useState(false);
  const [isSyncingDirectory, setIsSyncingDirectory] = useState(false);
  const [isImportingDirectory, setIsImportingDirectory] = useState(false);
  const [savingDirectoryGroupId, setSavingDirectoryGroupId] = useState<number | null>(null);
  const [deletingDirectoryMappingId, setDeletingDirectoryMappingId] = useState<number | null>(null);
  const [hasLoadedTechnicalAccess, setHasLoadedTechnicalAccess] = useState(false);
  const [hasLoadedDirectory, setHasLoadedDirectory] = useState(false);
  const hasLoadedTechnicalAccessRef = useRef(false);
  const hasLoadedDirectoryRef = useRef(false);

  useEffect(() => {
    hasLoadedTechnicalAccessRef.current = hasLoadedTechnicalAccess;
  }, [hasLoadedTechnicalAccess]);

  useEffect(() => {
    hasLoadedDirectoryRef.current = hasLoadedDirectory;
  }, [hasLoadedDirectory]);

  const loadTechnicalAccess = useCallback(async () => {
    setIsLoadingTechnicalAccess(true);
    setError(null);

    try {
      const [rolesData, groupsData, permissionsData, permissionAuditPage] = await Promise.all([
        getAdminRoles(),
        getAdminGroups(),
        getAdminPermissions(),
        getAdminPermissionAudit({ limit: 50 }),
      ]);
      setRoles(rolesData);
      setGroups(groupsData);
      setPermissions(permissionsData);
      setPermissionAuditEntries(permissionAuditPage.items);
      setPermissionAuditNextCursor(permissionAuditPage.nextCursor);
      setHasMorePermissionAudit(permissionAuditPage.hasMore);
      setHasLoadedTechnicalAccess(true);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Benutzerrechte und Gruppen konnten nicht geladen werden.";
      setError(message);
      setRoles([]);
      setGroups([]);
      setPermissions([]);
      setPermissionAuditEntries([]);
      setPermissionAuditNextCursor(null);
      setHasMorePermissionAudit(false);
    } finally {
      setIsLoadingTechnicalAccess(false);
    }
  }, [setError]);

  const loadDirectoryData = useCallback(async () => {
    setIsLoadingDirectory(true);
    setError(null);

    try {
      const [statusData, groupsData, identitiesData, auditPage, rolesData, responsibilityGapsData, pendingImportsData] = await Promise.all([
        getAdminDirectoryStatus(),
        getAdminDirectoryGroups(),
        getAdminDirectoryIdentities(25, 0),
        getAdminDirectoryAudit({ limit: 20 }),
        getAdminRoles(),
        getAdminDirectoryResponsibilityGaps(),
        getAdminDirectoryPendingImports(),
      ]);

      setDirectoryStatus(statusData);
      setDirectoryGroups(groupsData);
      setDirectoryIdentities(identitiesData);
      setDirectoryAuditEntries(auditPage.items);
      setDirectoryAuditNextCursor(auditPage.nextCursor);
      setHasMoreDirectoryAudit(auditPage.hasMore);
      setRoles(rolesData);
      setDirectoryResponsibilityGaps(responsibilityGapsData);
      setDirectoryPendingImports(pendingImportsData);
      setHasLoadedDirectory(true);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Verzeichnisdaten konnten nicht geladen werden.";
      setError(message);
      setDirectoryStatus(null);
      setDirectoryGroups([]);
      setDirectoryIdentities([]);
      setDirectoryAuditEntries([]);
      setDirectoryAuditNextCursor(null);
      setHasMoreDirectoryAudit(false);
      setDirectoryResponsibilityGaps(null);
      setDirectoryPendingImports(null);
    } finally {
      setIsLoadingDirectory(false);
    }
  }, [setError]);

  const handleLoadMorePermissionAudit = useCallback(async () => {
    if (!permissionAuditNextCursor || isLoadingMorePermissionAudit) return;
    setIsLoadingMorePermissionAudit(true);
    try {
      const page = await getAdminPermissionAudit({ limit: 50, cursor: permissionAuditNextCursor });
      setPermissionAuditEntries((prev) => [...prev, ...page.items]);
      setPermissionAuditNextCursor(page.nextCursor);
      setHasMorePermissionAudit(page.hasMore);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Weitere Eintraege konnten nicht geladen werden.";
      setError(message);
    } finally {
      setIsLoadingMorePermissionAudit(false);
    }
  }, [permissionAuditNextCursor, isLoadingMorePermissionAudit, setError]);

  const handleLoadMoreDirectoryAudit = useCallback(async () => {
    if (!directoryAuditNextCursor || isLoadingMoreDirectoryAudit) return;
    setIsLoadingMoreDirectoryAudit(true);
    try {
      const page = await getAdminDirectoryAudit({ limit: 50, cursor: directoryAuditNextCursor });
      setDirectoryAuditEntries((prev) => [...prev, ...page.items]);
      setDirectoryAuditNextCursor(page.nextCursor);
      setHasMoreDirectoryAudit(page.hasMore);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Weitere Eintraege konnten nicht geladen werden.";
      setError(message);
    } finally {
      setIsLoadingMoreDirectoryAudit(false);
    }
  }, [directoryAuditNextCursor, isLoadingMoreDirectoryAudit, setError]);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [
        usersData,
        departmentsPage,
        positionsPage,
        responsibilitiesPage,
        graphApplicationConfigurationData,
        notificationEmailConfigurationData,
      ] = await Promise.all([
        getAdminUsers(),
        getAdminDepartmentAssignments({ limit: 200 }),
        getAdminDepartmentPositions({ limit: 200 }),
        getAdminResponsibilityOwners({ limit: 200 }),
        getAdminGraphApplicationConfiguration(),
        getAdminNotificationEmailConfiguration(),
      ]);

      setUsers(usersData);
      setDepartmentAssignments(departmentsPage.items);
      setDepartmentPositions(positionsPage.items);
      setResponsibilityOwners(responsibilitiesPage.items);
      setGraphApplicationConfiguration(graphApplicationConfigurationData);
      setNotificationEmailConfiguration(notificationEmailConfigurationData);

      if (hasLoadedTechnicalAccessRef.current) {
        await loadTechnicalAccess();
      }

      if (hasLoadedDirectoryRef.current) {
        await loadDirectoryData();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Stammdaten konnten nicht geladen werden.";
      setError(message);
      setUsers([]);
      setDepartmentAssignments([]);
      setDepartmentPositions([]);
      setResponsibilityOwners([]);
      setGraphApplicationConfiguration(null);
      setNotificationEmailConfiguration(null);
      setDirectoryStatus(null);
      setDirectoryGroups([]);
      setDirectoryIdentities([]);
      setDirectoryAuditEntries([]);
    } finally {
      setIsLoading(false);
    }
  }, [loadDirectoryData, loadTechnicalAccess, setError, setGraphApplicationConfiguration, setNotificationEmailConfiguration]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (section !== "access" || hasLoadedTechnicalAccess) {
      return;
    }

    void loadTechnicalAccess();
  }, [hasLoadedTechnicalAccess, loadTechnicalAccess, section]);

  useEffect(() => {
    if (section !== "directory" || hasLoadedDirectory) {
      return;
    }

    void loadDirectoryData();
  }, [hasLoadedDirectory, loadDirectoryData, section]);

  const handleSyncDirectory = useCallback(async (groupPrefix: string | null) => {
    setIsSyncingDirectory(true);
    setNotice(null);
    setError(null);

    try {
      const result = await syncAdminDirectory(groupPrefix);
      await loadDirectoryData();
      if (result.status === "failed") {
        setError(result.errorMessage ?? "Verzeichnis-Sync fehlgeschlagen.");
      } else {
        setNotice(
          `Verzeichnis-Sync ${result.status === "partial" ? "teilweise" : "erfolgreich"}: ${result.groupsSynced} Gruppen, ${result.identitiesSynced} Identitäten, ${result.membershipsSynced} Mitgliedschaften.${result.appliedGroupPrefix ? ` Filter: ${result.appliedGroupPrefix}.` : " Ohne Filter."}`
        );
        if (result.errorMessage) {
          setError(result.errorMessage);
        }
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Verzeichnis-Sync konnte nicht gestartet werden.";
      setError(message);
    } finally {
      setIsSyncingDirectory(false);
    }
  }, [loadDirectoryData, setError, setNotice]);

  const handleCreateDirectoryMapping = useCallback(
    async (directoryGroupId: number, appRoleId: number, scope: string, scopeDepartmentId: number | null) => {
      setSavingDirectoryGroupId(directoryGroupId);
      setNotice(null);
      setError(null);

      try {
        const mapping = await createAdminDirectoryGroupRoleMapping({
          directoryGroupId,
          appRoleId,
          scope,
          scopeDepartmentId,
          isActive: true,
        });
        await loadDirectoryData();
        const scopeDepartmentName =
          departmentAssignments.find((department) => department.departmentId === scopeDepartmentId)?.departmentName ?? null;
        const scopeLabel = scope === "department" ? `Abteilung ${scopeDepartmentName ?? "unbekannt"}` : "global";
        setNotice(`Mapping gespeichert: ${mapping.appRoleName} wurde der Verzeichnisgruppe mit Scope ${scopeLabel} zugeordnet.`);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Mapping konnte nicht gespeichert werden.";
        setError(message);
      } finally {
        setSavingDirectoryGroupId(null);
      }
    },
    [departmentAssignments, loadDirectoryData, setError, setNotice]
  );

  const handleImportDirectoryIdentities = useCallback(
    async (directoryIdentityIds: number[]) => {
      setIsImportingDirectory(true);
      setNotice(null);
      setError(null);

      try {
        const result = await postAdminDirectoryImport(directoryIdentityIds);
        await loadDirectoryData();
        if (result.importedCount > 0) {
          setNotice(
            `${result.importedCount} ${result.importedCount === 1 ? "Person" : "Personen"} erfolgreich importiert.${result.failedCount > 0 ? ` ${result.failedCount} fehlgeschlagen.` : ""}`
          );
        }
        if (result.failedCount > 0 && result.importedCount === 0) {
          setError(`Import fehlgeschlagen: ${result.failed.map((f) => f.reason).join(", ")}`);
        }
      } catch (err) {
        const message = err instanceof Error ? err.message : "Import konnte nicht durchgeführt werden.";
        setError(message);
      } finally {
        setIsImportingDirectory(false);
      }
    },
    [loadDirectoryData, setError, setNotice]
  );

  const handleDeleteDirectoryMapping = useCallback(
    async (mappingId: number) => {
      setDeletingDirectoryMappingId(mappingId);
      setNotice(null);
      setError(null);

      try {
        await deleteAdminDirectoryGroupRoleMapping(mappingId);
        await loadDirectoryData();
        setNotice("Mapping wurde entfernt.");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Mapping konnte nicht entfernt werden.";
        setError(message);
      } finally {
        setDeletingDirectoryMappingId(null);
      }
    },
    [loadDirectoryData, setError, setNotice]
  );

  return {
    users,
    setUsers,
    roles,
    setRoles,
    groups,
    setGroups,
    permissions,
    permissionAuditEntries,
    setPermissionAuditEntries,
    hasMorePermissionAudit,
    isLoadingMorePermissionAudit,
    handleLoadMorePermissionAudit,
    directoryGroups,
    directoryIdentities,
    directoryAuditEntries,
    hasMoreDirectoryAudit,
    isLoadingMoreDirectoryAudit,
    handleLoadMoreDirectoryAudit,
    directoryStatus,
    directoryResponsibilityGaps,
    directoryPendingImports,
    departmentAssignments,
    setDepartmentAssignments,
    departmentPositions,
    setDepartmentPositions,
    responsibilityOwners,
    setResponsibilityOwners,
    isLoading,
    isLoadingTechnicalAccess,
    isLoadingDirectory,
    isSyncingDirectory,
    isImportingDirectory,
    savingDirectoryGroupId,
    deletingDirectoryMappingId,
    hasLoadedTechnicalAccess,
    reload,
    handleSyncDirectory,
    handleImportDirectoryIdentities,
    handleCreateDirectoryMapping,
    handleDeleteDirectoryMapping,
  };
}
