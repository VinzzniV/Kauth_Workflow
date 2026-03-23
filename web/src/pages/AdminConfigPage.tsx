// Administrative Konfigurationsseite fuer Benutzer, Rollen, Gruppen und Stammdaten.
import { useCallback, useEffect, useMemo, useState } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import {
  AdminCoreDataSummarySection,
  AdminDepartmentsSection,
  AdminNotificationEmailSection,
  AdminResponsibilitiesSection,
  AdminTechnicalAccessSection,
  AdminWorkflowConfigurationSection,
  AdminUsersSection,
} from "../components/admin-config/AdminConfigSections";
import {
  toNullableNumber,
  toNullableText,
  toggleId,
} from "../components/admin-config/adminConfigHelpers";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import {
  createAdminDepartment,
  createAdminUser,
  getAdminWorkflowConfig,
  deleteAdminDepartment,
  deleteAdminUser,
  getAdminDepartmentAssignments,
  getAdminGroups,
  getAdminNotificationEmailConfiguration,
  getAdminResponsibilityOwners,
  getAdminRoles,
  getAdminUsers,
  sendAdminNotificationEmailTest,
  updateAdminDepartmentAssignment,
  updateAdminGroupRoles,
  updateAdminNotificationEmailConfiguration,
  updateAdminResponsibilityOwner,
  updateAdminUserGroups,
  updateAdminUserMasterData,
  updateAdminUserRoles,
} from "../services/onboardingApi";
import type {
  AdminDepartmentAssignment,
  AdminGroup,
  AdminNotificationEmailConfiguration,
  AdminResponsibilityOwner,
  AdminRole,
  AdminUser,
} from "../types/auth";
import type { WorkflowConfig } from "../types/workflow";

export default function AdminConfigPage() {
  const { refreshCurrentUser } = useCurrentUser();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [groups, setGroups] = useState<AdminGroup[]>([]);
  const [departmentAssignments, setDepartmentAssignments] = useState<AdminDepartmentAssignment[]>([]);
  const [responsibilityOwners, setResponsibilityOwners] = useState<AdminResponsibilityOwner[]>([]);
  const [workflowConfig, setWorkflowConfig] = useState<WorkflowConfig | null>(null);
  const [notificationEmailConfiguration, setNotificationEmailConfiguration] =
    useState<AdminNotificationEmailConfiguration | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isLoadingTechnicalAccess, setIsLoadingTechnicalAccess] = useState<boolean>(false);
  const [isTechnicalAccessOpen, setIsTechnicalAccessOpen] = useState<boolean>(false);
  const [hasLoadedTechnicalAccess, setHasLoadedTechnicalAccess] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [userFormError, setUserFormError] = useState<string | null>(null);
  const [userFormNotice, setUserFormNotice] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [selectedUserRoleIds, setSelectedUserRoleIds] = useState<number[]>([]);
  const [selectedUserGroupIds, setSelectedUserGroupIds] = useState<number[]>([]);
  const [selectedGroupRoleIds, setSelectedGroupRoleIds] = useState<number[]>([]);
  const [userExternalKeyDraft, setUserExternalKeyDraft] = useState<string>("");
  const [userDisplayNameDraft, setUserDisplayNameDraft] = useState<string>("");
  const [userEmailDraft, setUserEmailDraft] = useState<string>("");
  const [userNotificationEmailDraft, setUserNotificationEmailDraft] = useState<string>("");
  const [userDepartmentIdDraft, setUserDepartmentIdDraft] = useState<string>("");
  const [userIsActiveDraft, setUserIsActiveDraft] = useState<boolean>(true);
  const [newDepartmentNameDraft, setNewDepartmentNameDraft] = useState<string>("");
  const [newUserExternalKeyDraft, setNewUserExternalKeyDraft] = useState<string>("");
  const [newUserDisplayNameDraft, setNewUserDisplayNameDraft] = useState<string>("");
  const [newUserEmailDraft, setNewUserEmailDraft] = useState<string>("");
  const [newUserNotificationEmailDraft, setNewUserNotificationEmailDraft] = useState<string>("");
  const [newUserDepartmentIdDraft, setNewUserDepartmentIdDraft] = useState<string>("");
  const [newUserIsActiveDraft, setNewUserIsActiveDraft] = useState<boolean>(true);
  const [notificationEnabledDraft, setNotificationEnabledDraft] = useState<boolean>(false);
  const [notificationTenantIdDraft, setNotificationTenantIdDraft] = useState<string>("");
  const [notificationClientIdDraft, setNotificationClientIdDraft] = useState<string>("");
  const [notificationClientSecretDraft, setNotificationClientSecretDraft] = useState<string>("");
  const [notificationSenderEmailDraft, setNotificationSenderEmailDraft] = useState<string>("");
  const [notificationFrontendBaseUrlDraft, setNotificationFrontendBaseUrlDraft] = useState<string>("");
  const [notificationTestRecipientDraft, setNotificationTestRecipientDraft] = useState<string>("");
  const [notificationSandboxRedirectDraft, setNotificationSandboxRedirectDraft] = useState<string>("");
  const [notificationNotifyOnWorkflowCreatedDraft, setNotificationNotifyOnWorkflowCreatedDraft] = useState<boolean>(true);
  const [notificationNotifyOnTaskReadyDraft, setNotificationNotifyOnTaskReadyDraft] = useState<boolean>(true);
  const [notificationNotifyOnWorkflowCompletedDraft, setNotificationNotifyOnWorkflowCompletedDraft] = useState<boolean>(true);
  const [departmentDrafts, setDepartmentDrafts] = useState<
    Record<number, { departmentLeadUserId: string; requirementOwnerUserId: string }>
  >({});
  const [responsibilityDrafts, setResponsibilityDrafts] = useState<
    Record<number, { appUserId: string; departmentId: string }>
  >({});
  const [isSavingUserMasterData, setIsSavingUserMasterData] = useState<boolean>(false);
  const [isSavingUserRoles, setIsSavingUserRoles] = useState<boolean>(false);
  const [isSavingUserGroups, setIsSavingUserGroups] = useState<boolean>(false);
  const [isSavingGroupRoles, setIsSavingGroupRoles] = useState<boolean>(false);
  const [isCreatingDepartment, setIsCreatingDepartment] = useState<boolean>(false);
  const [isCreatingUser, setIsCreatingUser] = useState<boolean>(false);
  const [deletingDepartmentId, setDeletingDepartmentId] = useState<number | null>(null);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);
  const [savingDepartmentId, setSavingDepartmentId] = useState<number | null>(null);
  const [savingResponsibilityId, setSavingResponsibilityId] = useState<number | null>(null);
  const [isSavingNotificationEmailConfiguration, setIsSavingNotificationEmailConfiguration] =
    useState<boolean>(false);
  const [isSendingNotificationEmailTest, setIsSendingNotificationEmailTest] = useState<boolean>(false);

  const loadTechnicalAccess = useCallback(async () => {
    setIsLoadingTechnicalAccess(true);
    setError(null);

    try {
      const [rolesData, groupsData] = await Promise.all([getAdminRoles(), getAdminGroups()]);
      setRoles(rolesData);
      setGroups(groupsData);
      setHasLoadedTechnicalAccess(true);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Benutzerrechte und Gruppen konnten nicht geladen werden.";
      setError(message);
      setRoles([]);
      setGroups([]);
    } finally {
      setIsLoadingTechnicalAccess(false);
    }
  }, []);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const workflowConfigPromise = getAdminWorkflowConfig().catch(() => null);
      const [usersData, departmentsData, responsibilitiesData, notificationEmailConfigurationData] = await Promise.all([
        getAdminUsers(),
        getAdminDepartmentAssignments(),
        getAdminResponsibilityOwners(),
        getAdminNotificationEmailConfiguration(),
      ]);

      setUsers(usersData);
      setDepartmentAssignments(departmentsData);
      setResponsibilityOwners(responsibilitiesData);
      setNotificationEmailConfiguration(notificationEmailConfigurationData);
      setWorkflowConfig(await workflowConfigPromise);

      if (hasLoadedTechnicalAccess) {
        await loadTechnicalAccess();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Stammdaten konnten nicht geladen werden.";
      setError(message);
      setUsers([]);
      setDepartmentAssignments([]);
      setResponsibilityOwners([]);
      setWorkflowConfig(null);
      setNotificationEmailConfiguration(null);
    } finally {
      setIsLoading(false);
    }
  }, [hasLoadedTechnicalAccess, loadTechnicalAccess]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (!isTechnicalAccessOpen || hasLoadedTechnicalAccess) {
      return;
    }

    void loadTechnicalAccess();
  }, [hasLoadedTechnicalAccess, isTechnicalAccessOpen, loadTechnicalAccess]);

  const sortedUsers = useMemo(
    () => users.slice().sort((left, right) => left.displayName.localeCompare(right.displayName, "de")),
    [users]
  );
  const eligibleSupervisorUsers = useMemo(
    () =>
      sortedUsers.filter(
        (user) =>
          user.isActive
          && user.hasManagerAccess
      ),
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
    () => departmentAssignments.slice().sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de")),
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
  const selectedUser = useMemo(() => users.find((user) => user.userId === selectedUserId) ?? null, [users, selectedUserId]);
  const selectedGroup = useMemo(() => groups.find((group) => group.groupId === selectedGroupId) ?? null, [groups, selectedGroupId]);

  const hasNotificationEmailDraftChanges = useMemo(() => {
    if (!notificationEmailConfiguration) {
      return false;
    }

    const hasClientSecretReplacement = notificationClientSecretDraft.trim().length > 0;

    return (
      notificationEnabledDraft !== notificationEmailConfiguration.enabled
      || toNullableText(notificationTenantIdDraft) !== notificationEmailConfiguration.tenantId
      || toNullableText(notificationClientIdDraft) !== notificationEmailConfiguration.clientId
      || hasClientSecretReplacement
      || toNullableText(notificationSenderEmailDraft) !== notificationEmailConfiguration.senderEmail
      || notificationFrontendBaseUrlDraft.trim() !== notificationEmailConfiguration.frontendBaseUrl
      || toNullableText(notificationTestRecipientDraft) !== notificationEmailConfiguration.testRecipientEmail
      || toNullableText(notificationSandboxRedirectDraft) !== notificationEmailConfiguration.sandboxRedirectEmail
      || notificationNotifyOnWorkflowCreatedDraft !== notificationEmailConfiguration.notifyOnWorkflowCreated
      || notificationNotifyOnTaskReadyDraft !== notificationEmailConfiguration.notifyOnTaskReady
      || notificationNotifyOnWorkflowCompletedDraft !== notificationEmailConfiguration.notifyOnWorkflowCompleted
    );
  }, [
    notificationClientIdDraft,
    notificationClientSecretDraft,
    notificationEmailConfiguration,
    notificationEnabledDraft,
    notificationFrontendBaseUrlDraft,
    notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft,
    notificationNotifyOnWorkflowCreatedDraft,
    notificationSandboxRedirectDraft,
    notificationSenderEmailDraft,
    notificationTenantIdDraft,
    notificationTestRecipientDraft,
  ]);

  const selectUser = useCallback((user: AdminUser) => {
    setSelectedUserId(user.userId);
    setSelectedUserRoleIds(user.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(user.groups.map((group) => group.groupId));
    setUserExternalKeyDraft(user.externalKey ?? "");
    setUserDisplayNameDraft(user.displayName);
    setUserEmailDraft(user.email);
    setUserNotificationEmailDraft(user.notificationEmail ?? "");
    setUserDepartmentIdDraft(user.departmentId ? String(user.departmentId) : "");
    setUserIsActiveDraft(user.isActive);
    setUserFormError(null);
    setUserFormNotice(null);
    setError(null);
    setNotice(null);
  }, []);

  const selectGroup = useCallback(
    (groupId: number | null) => {
      setSelectedGroupId(groupId);
      const group = groups.find((item) => item.groupId === groupId) ?? null;
      setSelectedGroupRoleIds(group ? group.roles.map((role) => role.roleId) : []);
      setNotice(null);
    },
    [groups]
  );

  useEffect(() => {
    if (!selectedUser) {
      setSelectedUserRoleIds([]);
      setSelectedUserGroupIds([]);
      setUserExternalKeyDraft("");
      setUserDisplayNameDraft("");
      setUserEmailDraft("");
      setUserNotificationEmailDraft("");
      setUserDepartmentIdDraft("");
      setUserIsActiveDraft(true);
      return;
    }

    setSelectedUserRoleIds(selectedUser.roles.map((role) => role.roleId));
    setSelectedUserGroupIds(selectedUser.groups.map((group) => group.groupId));
    setUserExternalKeyDraft(selectedUser.externalKey ?? "");
    setUserDisplayNameDraft(selectedUser.displayName);
    setUserEmailDraft(selectedUser.email);
    setUserNotificationEmailDraft(selectedUser.notificationEmail ?? "");
    setUserDepartmentIdDraft(selectedUser.departmentId ? String(selectedUser.departmentId) : "");
    setUserIsActiveDraft(selectedUser.isActive);
  }, [selectedUser]);

  useEffect(() => {
    if (!selectedGroup) {
      setSelectedGroupRoleIds([]);
      return;
    }

    setSelectedGroupRoleIds(selectedGroup.roles.map((role) => role.roleId));
  }, [selectedGroup]);

  useEffect(() => {
    setDepartmentDrafts(
      Object.fromEntries(
        departmentAssignments.map((item) => [
          item.departmentId,
          {
            departmentLeadUserId: item.departmentLeadUserId ? String(item.departmentLeadUserId) : "",
            requirementOwnerUserId: item.requirementOwnerUserId ? String(item.requirementOwnerUserId) : "",
          },
        ])
      )
    );
  }, [departmentAssignments]);

  useEffect(() => {
    setResponsibilityDrafts(
      Object.fromEntries(
        responsibilityOwners.map((item) => [
          item.responsibilityId,
          {
            appUserId: item.appUserId ? String(item.appUserId) : "",
            departmentId: item.departmentId ? String(item.departmentId) : "",
          },
        ])
      )
    );
  }, [responsibilityOwners]);

  useEffect(() => {
    if (!notificationEmailConfiguration) {
      setNotificationEnabledDraft(false);
      setNotificationTenantIdDraft("");
      setNotificationClientIdDraft("");
      setNotificationClientSecretDraft("");
      setNotificationSenderEmailDraft("");
      setNotificationFrontendBaseUrlDraft("");
      setNotificationTestRecipientDraft("");
      setNotificationSandboxRedirectDraft("");
      setNotificationNotifyOnWorkflowCreatedDraft(true);
      setNotificationNotifyOnTaskReadyDraft(true);
      setNotificationNotifyOnWorkflowCompletedDraft(true);
      return;
    }

    setNotificationEnabledDraft(notificationEmailConfiguration.enabled);
    setNotificationTenantIdDraft(notificationEmailConfiguration.tenantId ?? "");
    setNotificationClientIdDraft(notificationEmailConfiguration.clientId ?? "");
    setNotificationClientSecretDraft("");
    setNotificationSenderEmailDraft(notificationEmailConfiguration.senderEmail ?? "");
    setNotificationFrontendBaseUrlDraft(notificationEmailConfiguration.frontendBaseUrl);
    setNotificationTestRecipientDraft(notificationEmailConfiguration.testRecipientEmail ?? "");
    setNotificationSandboxRedirectDraft(notificationEmailConfiguration.sandboxRedirectEmail ?? "");
    setNotificationNotifyOnWorkflowCreatedDraft(notificationEmailConfiguration.notifyOnWorkflowCreated);
    setNotificationNotifyOnTaskReadyDraft(notificationEmailConfiguration.notifyOnTaskReady);
    setNotificationNotifyOnWorkflowCompletedDraft(notificationEmailConfiguration.notifyOnWorkflowCompleted);
  }, [notificationEmailConfiguration]);

  const saveNotificationEmailConfiguration = useCallback(async () => {
    setIsSavingNotificationEmailConfiguration(true);
    setNotice(null);
    setError(null);

    try {
      const clientSecret = toNullableText(notificationClientSecretDraft);
      const updatedConfiguration = await updateAdminNotificationEmailConfiguration({
        enabled: notificationEnabledDraft,
        tenantId: toNullableText(notificationTenantIdDraft),
        clientId: toNullableText(notificationClientIdDraft),
        ...(clientSecret ? { clientSecret } : {}),
        senderEmail: toNullableText(notificationSenderEmailDraft),
        frontendBaseUrl: notificationFrontendBaseUrlDraft.trim(),
        testRecipientEmail: toNullableText(notificationTestRecipientDraft),
        sandboxRedirectEmail: toNullableText(notificationSandboxRedirectDraft),
        notifyOnWorkflowCreated: notificationNotifyOnWorkflowCreatedDraft,
        notifyOnTaskReady: notificationNotifyOnTaskReadyDraft,
        notifyOnWorkflowCompleted: notificationNotifyOnWorkflowCompletedDraft,
      });
      setNotificationEmailConfiguration(updatedConfiguration);
      setNotificationClientSecretDraft("");
      setNotice("Mail-Konfiguration wurde gespeichert.");
    } catch (err) {
      const message = err instanceof Error ? err.message : "Mail-Konfiguration konnte nicht gespeichert werden.";
      setError(message);
    } finally {
      setIsSavingNotificationEmailConfiguration(false);
    }
  }, [
    notificationClientIdDraft,
    notificationClientSecretDraft,
    notificationEnabledDraft,
    notificationFrontendBaseUrlDraft,
    notificationNotifyOnTaskReadyDraft,
    notificationNotifyOnWorkflowCompletedDraft,
    notificationNotifyOnWorkflowCreatedDraft,
    notificationSandboxRedirectDraft,
    notificationSenderEmailDraft,
    notificationTenantIdDraft,
    notificationTestRecipientDraft,
  ]);

  const sendNotificationEmailTest = useCallback(async () => {
    setIsSendingNotificationEmailTest(true);
    setNotice(null);
    setError(null);

    try {
      const response = await sendAdminNotificationEmailTest(toNullableText(notificationTestRecipientDraft));
      setNotificationEmailConfiguration(response.configuration);
      setNotice(response.result.message);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Testmail konnte nicht versendet werden.";
      setError(message);
    } finally {
      setIsSendingNotificationEmailTest(false);
    }
  }, [notificationTestRecipientDraft]);

  const saveUserMasterData = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserMasterData(true);
    setUserFormError(null);
    setUserFormNotice(null);
    setNotice(null);
    setError(null);

    try {
      const updatedUser = await updateAdminUserMasterData(
        selectedUser.userId,
        userExternalKeyDraft.trim() || null,
        userDisplayNameDraft,
        userEmailDraft,
        toNullableText(userNotificationEmailDraft),
        toNullableNumber(userDepartmentIdDraft),
        userIsActiveDraft
      );

      const freshUsers = await getAdminUsers();
      setUsers(freshUsers);
      setSelectedUserId(updatedUser.userId);
      await refreshCurrentUser();
      setUserFormNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
      setNotice(`Personenstammdaten für ${updatedUser.displayName} wurden gespeichert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Personenstammdaten konnten nicht gespeichert werden.";
      setUserFormError(message);
      setError(message);
    } finally {
      setIsSavingUserMasterData(false);
    }
  }, [
    refreshCurrentUser,
    selectedUser,
    userDepartmentIdDraft,
    userDisplayNameDraft,
    userEmailDraft,
    userNotificationEmailDraft,
    userExternalKeyDraft,
    userIsActiveDraft,
  ]);

  const createDepartment = useCallback(async () => {
    setIsCreatingDepartment(true);
    setNotice(null);
    setError(null);

    try {
      const createdDepartment = await createAdminDepartment(newDepartmentNameDraft);
      setDepartmentAssignments((current) =>
        current.concat(createdDepartment).sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de"))
      );
      setNewDepartmentNameDraft("");
      setNotice(`Abteilung ${createdDepartment.departmentName} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abteilung konnte nicht angelegt werden.";
      setError(message);
    } finally {
      setIsCreatingDepartment(false);
    }
  }, [newDepartmentNameDraft]);

  const createUser = useCallback(async () => {
    setIsCreatingUser(true);
    setNotice(null);
    setError(null);

    try {
      const createdUser = await createAdminUser({
        externalKey: newUserExternalKeyDraft.trim() || null,
        displayName: newUserDisplayNameDraft,
        email: newUserEmailDraft,
        notificationEmail: toNullableText(newUserNotificationEmailDraft),
        departmentId: toNullableNumber(newUserDepartmentIdDraft),
        isActive: newUserIsActiveDraft,
      });
      setUsers((current) =>
        current.concat(createdUser).sort((left, right) => left.displayName.localeCompare(right.displayName, "de"))
      );
      setSelectedUserId(createdUser.userId);
      setNewUserExternalKeyDraft("");
      setNewUserDisplayNameDraft("");
      setNewUserEmailDraft("");
      setNewUserNotificationEmailDraft("");
      setNewUserDepartmentIdDraft("");
      setNewUserIsActiveDraft(true);
      setNotice(`Person ${createdUser.displayName} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht angelegt werden.";
      setError(message);
    } finally {
      setIsCreatingUser(false);
    }
  }, [
    newUserDepartmentIdDraft,
    newUserDisplayNameDraft,
    newUserEmailDraft,
    newUserNotificationEmailDraft,
    newUserExternalKeyDraft,
    newUserIsActiveDraft,
  ]);

  const removeDepartment = useCallback(async (department: AdminDepartmentAssignment) => {
    if (typeof window !== "undefined" && !window.confirm(`Abteilung "${department.departmentName}" wirklich löschen?`)) {
      return;
    }

    setDeletingDepartmentId(department.departmentId);
    setNotice(null);
    setError(null);

    try {
      await deleteAdminDepartment(department.departmentId);
      await reload();
      setNotice(`Abteilung ${department.departmentName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abteilung konnte nicht gelöscht werden.";
      setError(message);
    } finally {
      setDeletingDepartmentId(null);
    }
  }, [reload]);

  const removeUser = useCallback(async (user: AdminUser) => {
    if (typeof window !== "undefined" && !window.confirm(`Person "${user.displayName}" wirklich löschen?`)) {
      return;
    }

    setDeletingUserId(user.userId);
    setNotice(null);
    setError(null);

    try {
      await deleteAdminUser(user.userId);
      if (selectedUserId === user.userId) {
        setSelectedUserId(null);
      }
      await reload();
      setNotice(`Person ${user.displayName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Person konnte nicht gelöscht werden.";
      setError(message);
    } finally {
      setDeletingUserId(null);
    }
  }, [reload, selectedUserId]);

  const saveUserRoles = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserRoles(true);
    setNotice(null);
    setError(null);

    try {
      const updatedUser = await updateAdminUserRoles(selectedUser.userId, selectedUserRoleIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setNotice(`Rollen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Rollen konnten nicht aktualisiert werden.";
      setError(message);
    } finally {
      setIsSavingUserRoles(false);
    }
  }, [selectedUser, selectedUserRoleIds]);

  const saveUserGroups = useCallback(async () => {
    if (!selectedUser) {
      return;
    }

    setIsSavingUserGroups(true);
    setNotice(null);
    setError(null);

    try {
      const updatedUser = await updateAdminUserGroups(selectedUser.userId, selectedUserGroupIds);
      setUsers((current) => current.map((user) => (user.userId === updatedUser.userId ? updatedUser : user)));
      setNotice(`Gruppen für ${updatedUser.displayName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppen konnten nicht aktualisiert werden.";
      setError(message);
    } finally {
      setIsSavingUserGroups(false);
    }
  }, [selectedUser, selectedUserGroupIds]);

  const saveGroupRoles = useCallback(async () => {
    if (!selectedGroup) {
      return;
    }

    setIsSavingGroupRoles(true);
    setNotice(null);
    setError(null);

    try {
      const updatedGroup = await updateAdminGroupRoles(selectedGroup.groupId, selectedGroupRoleIds);
      setGroups((current) => current.map((group) => (group.groupId === updatedGroup.groupId ? updatedGroup : group)));
      setNotice(`Gruppenrollen für ${updatedGroup.groupName} wurden aktualisiert.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Gruppenrollen konnten nicht aktualisiert werden.";
      setError(message);
    } finally {
      setIsSavingGroupRoles(false);
    }
  }, [selectedGroup, selectedGroupRoleIds]);

  const saveDepartmentAssignment = useCallback(async (departmentId: number) => {
    const draft = departmentDrafts[departmentId];
    if (!draft) {
      return;
    }

    setSavingDepartmentId(departmentId);
    setNotice(null);
    setError(null);

    try {
      const updatedAssignment = await updateAdminDepartmentAssignment(
        departmentId,
        toNullableNumber(draft.departmentLeadUserId),
        toNullableNumber(draft.requirementOwnerUserId)
      );
      setDepartmentAssignments((current) =>
        current.map((item) => (item.departmentId === updatedAssignment.departmentId ? updatedAssignment : item))
      );
      setNotice(`Zuständigkeiten für ${updatedAssignment.departmentName} wurden gespeichert.`);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Abteilungs-Zuständigkeit konnte nicht gespeichert werden.";
      setError(message);
    } finally {
      setSavingDepartmentId(null);
    }
  }, [departmentDrafts]);

  const saveResponsibilityAssignment = useCallback(async (responsibilityId: number) => {
    const draft = responsibilityDrafts[responsibilityId] ?? { appUserId: "", departmentId: "" };

    setSavingResponsibilityId(responsibilityId);
    setNotice(null);
    setError(null);

    try {
      const updatedAssignment = await updateAdminResponsibilityOwner(
        responsibilityId,
        toNullableNumber(draft.appUserId),
        toNullableNumber(draft.departmentId)
      );
      setResponsibilityOwners((current) =>
        current.map((item) =>
          item.responsibilityId === updatedAssignment.responsibilityId ? updatedAssignment : item
        )
      );
      setNotice(`Zuständigkeit für ${updatedAssignment.responsibilityName} wurde gespeichert.`);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Fachliche Zuständigkeit konnte nicht gespeichert werden.";
      setError(message);
    } finally {
      setSavingResponsibilityId(null);
    }
  }, [responsibilityDrafts]);

  return (
    <main className="onboarding-shell">
      <div className="page-container">
        <PageHeader
          title="Stammdaten pflegen"
          description="Pflegen Sie Mail-Konfiguration, Abteilungen, Personen, Anforderungsverantwortung je Abteilung und fachliche Zuständigkeiten direkt in der Anwendung. Workflow-Definitionen werden hier transparent gemacht, der eigentliche Aufgabenplan bleibt derzeit bewusst read-only."
        />

        <AdminNotificationEmailSection
          notificationEmailConfiguration={notificationEmailConfiguration}
          notificationEnabledDraft={notificationEnabledDraft}
          notificationTenantIdDraft={notificationTenantIdDraft}
          notificationClientIdDraft={notificationClientIdDraft}
          notificationClientSecretDraft={notificationClientSecretDraft}
          notificationSenderEmailDraft={notificationSenderEmailDraft}
          notificationFrontendBaseUrlDraft={notificationFrontendBaseUrlDraft}
          notificationTestRecipientDraft={notificationTestRecipientDraft}
          notificationSandboxRedirectDraft={notificationSandboxRedirectDraft}
          notificationNotifyOnWorkflowCreatedDraft={notificationNotifyOnWorkflowCreatedDraft}
          notificationNotifyOnTaskReadyDraft={notificationNotifyOnTaskReadyDraft}
          notificationNotifyOnWorkflowCompletedDraft={notificationNotifyOnWorkflowCompletedDraft}
          isSavingNotificationEmailConfiguration={isSavingNotificationEmailConfiguration}
          isSendingNotificationEmailTest={isSendingNotificationEmailTest}
          isLoading={isLoading}
          hasNotificationEmailDraftChanges={hasNotificationEmailDraftChanges}
          onNotificationEnabledChange={setNotificationEnabledDraft}
          onNotificationTenantIdChange={setNotificationTenantIdDraft}
          onNotificationClientIdChange={setNotificationClientIdDraft}
          onNotificationClientSecretChange={setNotificationClientSecretDraft}
          onNotificationSenderEmailChange={setNotificationSenderEmailDraft}
          onNotificationFrontendBaseUrlChange={setNotificationFrontendBaseUrlDraft}
          onNotificationTestRecipientChange={setNotificationTestRecipientDraft}
          onNotificationSandboxRedirectChange={setNotificationSandboxRedirectDraft}
          onNotificationNotifyOnWorkflowCreatedChange={setNotificationNotifyOnWorkflowCreatedDraft}
          onNotificationNotifyOnTaskReadyChange={setNotificationNotifyOnTaskReadyDraft}
          onNotificationNotifyOnWorkflowCompletedChange={setNotificationNotifyOnWorkflowCompletedDraft}
          onSave={saveNotificationEmailConfiguration}
          onSendTest={sendNotificationEmailTest}
        />

        <AdminCoreDataSummarySection
          departmentCount={departmentAssignments.length}
          userCount={users.length}
          responsibilityCount={responsibilityOwners.length}
          error={error}
          notice={notice}
          isLoading={isLoading}
          isLoadingTechnicalAccess={isLoadingTechnicalAccess}
          isTechnicalAccessOpen={isTechnicalAccessOpen}
          onReload={reload}
          onToggleTechnicalAccess={() => setIsTechnicalAccessOpen((current) => !current)}
        />

        <AdminWorkflowConfigurationSection workflowConfig={workflowConfig} isLoading={isLoading} />

        {isLoading ? <LoadingState title="Stammdaten werden geladen..." /> : null}
        {!isLoading && error && users.length === 0 && departmentAssignments.length === 0 && responsibilityOwners.length === 0 ? (
          <EmptyState title="Stammdaten konnten nicht geladen werden." description={error} />
        ) : null}

        {!isLoading && (!error || users.length > 0 || departmentAssignments.length > 0 || responsibilityOwners.length > 0) ? (
          <>
            <AdminDepartmentsSection
              sortedDepartments={sortedDepartments}
              sortedUsers={sortedUsers}
              eligibleSupervisorUsers={eligibleSupervisorUsers}
              departmentDrafts={departmentDrafts}
              newDepartmentNameDraft={newDepartmentNameDraft}
              isCreatingDepartment={isCreatingDepartment}
              savingDepartmentId={savingDepartmentId}
              deletingDepartmentId={deletingDepartmentId}
              onNewDepartmentNameChange={setNewDepartmentNameDraft}
              onDepartmentDraftChange={(departmentId, draft) =>
                setDepartmentDrafts((current) => ({ ...current, [departmentId]: draft }))
              }
              onCreateDepartment={createDepartment}
              onSaveDepartmentAssignment={saveDepartmentAssignment}
              onRemoveDepartment={removeDepartment}
            />

            <AdminUsersSection
              sortedUsers={sortedUsers}
              sortedDepartments={sortedDepartments}
              selectedUserId={selectedUserId}
              selectedUser={selectedUser}
              newUserDisplayNameDraft={newUserDisplayNameDraft}
              newUserEmailDraft={newUserEmailDraft}
              newUserNotificationEmailDraft={newUserNotificationEmailDraft}
              newUserExternalKeyDraft={newUserExternalKeyDraft}
              newUserDepartmentIdDraft={newUserDepartmentIdDraft}
              newUserIsActiveDraft={newUserIsActiveDraft}
              userDisplayNameDraft={userDisplayNameDraft}
              userEmailDraft={userEmailDraft}
              userNotificationEmailDraft={userNotificationEmailDraft}
              userExternalKeyDraft={userExternalKeyDraft}
              userDepartmentIdDraft={userDepartmentIdDraft}
              userIsActiveDraft={userIsActiveDraft}
              userFormError={userFormError}
              userFormNotice={userFormNotice}
              isCreatingUser={isCreatingUser}
              isSavingUserMasterData={isSavingUserMasterData}
              deletingUserId={deletingUserId}
              onSelectUser={selectUser}
              onNewUserDisplayNameChange={setNewUserDisplayNameDraft}
              onNewUserEmailChange={setNewUserEmailDraft}
              onNewUserNotificationEmailChange={setNewUserNotificationEmailDraft}
              onNewUserExternalKeyChange={setNewUserExternalKeyDraft}
              onNewUserDepartmentIdChange={setNewUserDepartmentIdDraft}
              onNewUserIsActiveChange={setNewUserIsActiveDraft}
              onUserDisplayNameChange={setUserDisplayNameDraft}
              onUserEmailChange={setUserEmailDraft}
              onUserNotificationEmailChange={setUserNotificationEmailDraft}
              onUserExternalKeyChange={setUserExternalKeyDraft}
              onUserDepartmentIdChange={setUserDepartmentIdDraft}
              onUserIsActiveChange={setUserIsActiveDraft}
              onCreateUser={createUser}
              onSaveUserMasterData={saveUserMasterData}
              onRemoveUser={removeUser}
            />

            <AdminResponsibilitiesSection
              sortedResponsibilities={sortedResponsibilities}
              sortedDepartments={sortedDepartments}
              sortedUsers={sortedUsers}
              responsibilityDrafts={responsibilityDrafts}
              savingResponsibilityId={savingResponsibilityId}
              onResponsibilityDraftChange={(responsibilityId, draft) =>
                setResponsibilityDrafts((current) => ({ ...current, [responsibilityId]: draft }))
              }
              onSaveResponsibilityAssignment={saveResponsibilityAssignment}
            />

            <AdminTechnicalAccessSection
              isTechnicalAccessOpen={isTechnicalAccessOpen}
              isLoadingTechnicalAccess={isLoadingTechnicalAccess}
              selectedUser={selectedUser}
              selectedUserRoleIds={selectedUserRoleIds}
              selectedUserGroupIds={selectedUserGroupIds}
              selectedGroupId={selectedGroupId}
              selectedGroup={selectedGroup}
              selectedGroupRoleIds={selectedGroupRoleIds}
              sortedRoles={sortedRoles}
              groups={groups}
              isSavingUserRoles={isSavingUserRoles}
              isSavingUserGroups={isSavingUserGroups}
              isSavingGroupRoles={isSavingGroupRoles}
              onToggleUserRole={(roleId) => setSelectedUserRoleIds((current) => toggleId(current, roleId))}
              onToggleUserGroup={(groupId) => setSelectedUserGroupIds((current) => toggleId(current, groupId))}
              onSelectGroup={selectGroup}
              onToggleGroupRole={(roleId) => setSelectedGroupRoleIds((current) => toggleId(current, roleId))}
              onSaveUserRoles={saveUserRoles}
              onSaveUserGroups={saveUserGroups}
              onSaveGroupRoles={saveGroupRoles}
            />
          </>
        ) : null}
      </div>
    </main>
  );
}
