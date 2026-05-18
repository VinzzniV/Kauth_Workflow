// Frontend-Typen fuer Dev-Simulations-Login, aktuellen Benutzer und Admin-Stammdaten.
export type SimulationLoginUserOption = {
  userId: number;
  username: string;
  displayName: string;
  email: string;
  departmentName: string | null;
  roleKeys: string[];
};

export type Me = {
  username: string;
  displayName: string;
  email: string;
  roles: string[];
  groups: string[];
  permissions: string[];
  permissionScopes: PermissionScope[];
  canAccessSupervisorStep: boolean;
  directorySynced: boolean;
  departmentSource: string;
  departmentOverrideActive: boolean;
};

export type SimulationLoginResponse = {
  token: string;
  expiresAtUtc: string;
  user: Me;
};

// Admin-Daten werden separat modelliert, weil sie in der Konfigurationsseite bearbeitet werden.
export type AdminRole = {
  roleId: number;
  roleKey: string;
  roleName: string;
  roleKind: string;
  departmentId: number | null;
  departmentName: string | null;
  scope: string;
  scopeDepartmentId: number | null;
  scopeDepartmentName: string | null;
  isActive: boolean;
  permissions: AdminPermission[];
};

export type AdminGroupRef = {
  groupId: number;
  groupKey: string;
  groupName: string;
  description: string | null;
  isActive: boolean;
};

export type AdminGroup = AdminGroupRef & {
  roles: AdminRole[];
};

export type AdminUser = {
  userId: number;
  externalKey: string | null;
  displayName: string;
  email: string;
  notificationEmail: string | null;
  isActive: boolean;
  hasManagerAccess: boolean;
  canAccessSupervisorStep?: boolean;
  departmentId: number | null;
  departmentName: string | null;
  directorySynced: boolean;
  departmentSource: string;
  departmentOverrideActive: boolean;
  isTechnicalActor?: boolean;
  directoryIdentityId: number | null;
  userPrincipalName: string | null;
  directoryDisplayName: string | null;
  roles: AdminRole[];
  groups: AdminGroupRef[];
  effectiveRoles: AdminRole[];
  permissionOverrides: AdminPermissionOverride[];
  effectivePermissions: AdminPermissionGrant[];
};

export type PermissionScope = {
  permissionKey: string;
  scope: string;
  scopeDepartmentId: number | null;
  scopeDepartmentName: string | null;
};

export type AdminPermission = {
  permissionId: number;
  permissionKey: string;
  permissionName: string;
  description: string | null;
  scopeKind: string;
  category: string;
  isActive: boolean;
};

export type AdminPermissionGrant = {
  permissionId: number;
  permissionKey: string;
  permissionName: string;
  scope: string;
  scopeDepartmentId: number | null;
  scopeDepartmentName: string | null;
};

export type AdminPermissionOverride = AdminPermissionGrant & {
  overrideId: number;
  effect: string;
};

export type AdminDepartmentAssignment = {
  departmentId: number;
  departmentName: string;
  departmentLeadUserId: number | null;
  departmentLeadDisplayName: string | null;
  requirementOwnerUserId: number | null;
  requirementOwnerDisplayName: string | null;
  assignmentSource: string;
  syncState: string;
  syncDetail: string | null;
  updatedAt: string | null;
};

export type AdminResponsibilityOwner = {
  responsibilityId: number;
  responsibilityKey: string;
  systemKey: string | null;
  responsibilityName: string;
  responsibilityType: string;
  departmentId: number | null;
  departmentName: string | null;
  appUserId: number | null;
  appUserDisplayName: string | null;
  updatedAt: string | null;
};

export type AdminDirectoryGroupRoleMapping = {
  mappingId: number;
  directoryGroupId: number;
  appRoleId: number;
  appRoleKey: string;
  appRoleName: string;
  appRoleKind: string;
  roleDepartmentId: number | null;
  roleDepartmentName: string | null;
  scope: string;
  scopeDepartmentId: number | null;
  isActive: boolean;
};

export type AdminDirectoryGroup = {
  directoryGroupId: number;
  externalGroupId: string;
  displayName: string;
  description: string | null;
  lastSyncedAt: string | null;
  memberCount: number;
  roleMappings: AdminDirectoryGroupRoleMapping[];
};

export type AdminDirectoryIdentity = {
  directoryIdentityId: number;
  entraObjectId: string;
  userPrincipalName: string;
  mail: string | null;
  displayName: string;
  accountEnabled: boolean;
  appUserId: number | null;
  appUserDisplayName: string | null;
  lastSyncedAt: string | null;
};

export type AdminDirectorySyncStatus = {
  lastSyncAt: string | null;
  lastSyncStatus: string | null;
  totalGroups: number;
  totalIdentities: number;
  totalMappings: number;
  lastError: string | null;
  configuredGroupPrefix: string | null;
};

export type AdminDirectorySyncResult = {
  status: string;
  groupsSynced: number;
  identitiesSynced: number;
  membershipsSynced: number;
  errorMessage: string | null;
  appliedGroupPrefix: string | null;
};

export type AdminGraphApplicationConfiguration = {
  tenantId: string | null;
  clientId: string | null;
  hasClientSecret: boolean;
  updatedAt: string | null;
  configurationSource: "runtime";
  configurationStatus: "ready" | "incomplete";
  configurationMessage: string | null;
};

export type AdminDirectoryMappingAuditEntry = {
  auditEntryId: number;
  actorUserId: number | null;
  actorDisplayName: string | null;
  eventType: string;
  entityType: string;
  detail: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
};

export type DirectoryPendingImport = {
  directoryIdentityId: number;
  entraObjectId: string;
  displayName: string;
  mail: string | null;
  userPrincipalName: string;
  departmentName: string | null;
  previewDepartmentId: number | null;
  groupNames: string[];
  previewRoleKeys: string[];
};

export type DirectoryPendingImports = {
  pendingImports: DirectoryPendingImport[];
  totalCount: number;
};

export type DirectoryImportResult = {
  importedCount: number;
  failedCount: number;
  imported: Array<{ directoryIdentityId: number; appUserId: number; displayName: string }>;
  failed: Array<{ directoryIdentityId: number; reason: string }>;
};

// A2: Entra-Identitaet ohne people-Record — Auswahlelement im Import-UI.
export type UnlinkedDirectoryIdentity = {
  directoryIdentityId: number;
  entraObjectId: string;
  displayName: string;
  mail: string | null;
  userPrincipalName: string | null;
  departmentName: string | null;
  previewDepartmentId: number | null;
  employeeNumber: number | null;
  jobTitle: string | null;
  accountEnabled: boolean;
  appUserId: number | null;
  hasLinkedAppUser: boolean;
};

// A2: Ergebnis des POST /admin/people/import-from-directory.
export type ImportPeopleFromDirectoryResult = {
  createdCount: number;
  linkedCount: number;
  skippedCount: number;
  results: Array<{
    directoryIdentityId: number;
    displayName: string;
    outcome: "created" | "linked" | "skipped";
    personId: number | null;
    skipReason: string | null;
  }>;
};

export type DirectoryResponsibilityCandidate = {
  appUserId: number;
  displayName: string;
  mail: string | null;
};

export type DirectoryResponsibilityGapEntry = {
  departmentId: number;
  departmentName: string;
  entraGroupName: string | null;
  assignedLeadPersonId: number | null;
  candidatesInEntra: number;
  candidates: DirectoryResponsibilityCandidate[];
};

export type DirectoryResponsibilityGaps = {
  gaps: DirectoryResponsibilityGapEntry[];
  totalUnassignedDepartments: number;
  totalCandidatesNotYetAssigned: number;
};

export type AdminPermissionAuditEntry = {
  auditEntryId: number;
  actorUserId: number | null;
  actorDisplayName: string | null;
  eventType: string;
  entityType: string;
  detail: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
};

export type AdminNotificationEmailConfiguration = {
  enabled: boolean;
  mode: "enabled" | "disabled" | "sandbox";
  senderEmail: string | null;
  frontendBaseUrl: string;
  testRecipientEmail: string | null;
  sandboxRedirectEmail: string | null;
  notifyOnWorkflowCreated: boolean;
  notifyOnTaskReady: boolean;
  notifyOnWorkflowCompleted: boolean;
  lastTestStatus: "never" | "succeeded" | "failed" | "disabled";
  lastTestAt: string | null;
  lastError: string | null;
  updatedAt: string | null;
  hasClientSecret: boolean;
  configurationStatus: "disabled" | "ready" | "incomplete";
  configurationMessage: string | null;
};

export type AdminNotificationEmailTestResult = {
  success: boolean;
  status: "succeeded" | "failed" | "disabled";
  message: string;
  recipientEmail: string;
};

export type AdminNotificationEmailTestResponse = {
  configuration: AdminNotificationEmailConfiguration;
  result: AdminNotificationEmailTestResult;
};

export type AdminNotificationTemplatePlaceholder = {
  key: string;
  label: string;
  description: string;
};

export type AdminNotificationTemplate = {
  templateKey: string;
  displayName: string;
  triggerDescription: string;
  subjectTemplate: string;
  bodyTemplate: string;
  isSystemLocked: boolean;
  updatedAt: string | null;
  previewTargetType: "workflow" | "rotation_plan";
  placeholders: AdminNotificationTemplatePlaceholder[];
};

export type AdminNotificationTemplateWorkflowPreviewTarget = {
  workflowUid: string;
  displayName: string;
  processName: string;
  departmentName: string;
  workflowStatus: string;
  createdAt: string;
};

export type AdminNotificationTemplateRotationPlanPreviewTarget = {
  rotationPlanId: number;
  title: string;
  displayName: string;
  departmentName: string | null;
  status: string;
  updatedAt: string;
};

export type AdminNotificationTemplatePreviewTarget = {
  targetType: "workflow" | "rotation_plan";
  workflowUid: string | null;
  rotationPlanId: number | null;
  primaryLabel: string;
  secondaryLabel: string;
  status: string;
};

export type AdminNotificationTemplatePreviewRecipient = {
  recipientUserId: number | null;
  name: string;
  email: string;
};

export type AdminNotificationTemplatePlaceholderValue = {
  key: string;
  value: string;
};

export type AdminNotificationTemplatePreviewVariant = {
  recipient: AdminNotificationTemplatePreviewRecipient;
  renderedSubject: string;
  renderedTextBody: string;
  renderedHtmlBody: string;
  placeholderValues: AdminNotificationTemplatePlaceholderValue[];
};

export type AdminNotificationTemplatePreviewResponse = {
  templateKey: string;
  displayName: string;
  triggerDescription: string;
  previewTargetType: "workflow" | "rotation_plan";
  isCurrentlyTriggerable: boolean;
  blockingReason: string | null;
  target: AdminNotificationTemplatePreviewTarget;
  variants: AdminNotificationTemplatePreviewVariant[];
};

export type AdminSystemLogEntry = {
  id: number;
  createdAt: string;
  severity: "info" | "warning" | "error" | string;
  source: string;
  category: string;
  eventKey: string;
  message: string;
  userMessage: string | null;
  actorUserId: number | null;
  actorDisplayName: string | null;
  clientRoute: string | null;
  clientFunction: string | null;
  httpMethod: string | null;
  httpPath: string | null;
  httpStatus: number | null;
  traceIdentifier: string | null;
  workflowUid: string | null;
  rotationPlanId: number | null;
  taskRef: string | null;
  entityType: string | null;
  entityId: string | null;
  details: unknown | null;
};

export type AdminSystemLogSourceCount = {
  source: string;
  count: number;
};

export type AdminSystemLogSummary = {
  totalCount: number;
  infoCount: number;
  warningCount: number;
  errorCount: number;
  sources: AdminSystemLogSourceCount[];
};

export type ClientLogEventRequest = {
  severity?: "info" | "warning" | "error" | string;
  source?: string | null;
  category?: string | null;
  eventKey?: string | null;
  message?: string | null;
  userMessage?: string | null;
  clientRoute?: string | null;
  clientFunction?: string | null;
  httpMethod?: string | null;
  httpPath?: string | null;
  httpStatus?: number | null;
  traceIdentifier?: string | null;
  workflowUid?: string | null;
  rotationPlanId?: number | null;
  taskRef?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  details?: unknown | null;
};

// LA5: Frontend-Typen heissen AdminTaskSpec*; Backend sendet weiter `templateKey`/
// `taskTemplateId` ueber die Wire (siehe BackendAdminTaskTemplateDto in backendDtos.ts
// + Mapper in services/api/mappers.ts). Spaltennamen im Backend-Schema sind bereits
// `workflow_node_task_specs`; nur die Wire-DTOs bewahren die alten Property-Namen
// fuer API-Kompatibilitaet.

// Z12-2.2: Admin Runtime Health (GET /admin/runtime-health)
export type RuntimeHealthSeverity = "ok" | "warning" | "critical" | "unknown";

export type AdminRuntimeHealth = {
  generatedAt: string;
  overallSeverity: RuntimeHealthSeverity;
  application: ApplicationRuntimeHealth;
  dependencies: DependenciesRuntimeHealth;
  directory: DirectoryRuntimeHealth;
  storage: StorageRuntimeHealth[];
  host: HostRuntimeHealth | null;
  automationFailures: RuntimeFailuresHealth;
  notificationFailures: RuntimeFailuresHealth;
};

export type RuntimeFailuresHealth = {
  severity: RuntimeHealthSeverity;
  windowHours: number;
  totalCount: number;
  recentFailures: RuntimeFailureItem[];
};

export type RuntimeFailureItem = {
  id: number;
  occurredAt: string;
  label: string;
  errorMessage: string | null;
};

export type ApplicationRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  processStartedAt: string;
  uptimeSeconds: number;
  managedHeapBytes: number;
  managedHeapHighThresholdBytes: number | null;
  workingSetBytes: number;
  threadPool: ThreadPoolRuntimeHealth | null;
};

export type ThreadPoolRuntimeHealth = {
  workerThreadsAvailable: number;
  completionPortThreadsAvailable: number;
};

export type DependenciesRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  database: DependencyRuntimeHealth;
  auth: AuthDependencyRuntimeHealth;
  mail: MailDependencyRuntimeHealth;
};

export type DependencyRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  reachable: boolean;
  lastCheckedAt: string;
  latencyMs: number | null;
  lastError: string | null;
};

export type AuthDependencyRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  mode: string;
  reachability: string;
  lastCheckedAt: string;
  latencyMs: number | null;
  lastError: string | null;
};

export type MailDependencyRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  mode: string;
  configurationStatus: string;
  lastProbeAt: string | null;
  lastProbeStatus: string;
};

export type DirectoryRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  lastSyncAt: string | null;
  lastSyncStatus: string;
  lastError: string | null;
  nextScheduledSyncAt: string | null;
  pendingImportsCount: number;
};

export type StorageRuntimeHealth = {
  label: string;
  path: string;
  totalBytes: number;
  freeBytes: number;
  usedPercent: number;
  severity: RuntimeHealthSeverity;
};

// null wenn HOST_RUNTIME_HEALTH_ENABLED=false, nicht Linux, oder Metriken nicht lesbar
export type HostRuntimeHealth = {
  severity: RuntimeHealthSeverity;
  uptimeSeconds: number;
  loadAverage1m: number;
  memTotalBytes: number;
  memAvailableBytes: number;
  memUsedPercent: number;
  rootFsTotalBytes: number;
  rootFsFreeBytes: number;
  rootFsUsedPercent: number;
  zombieProcessCount: number;
};
export type AdminTaskSpec = {
  id: number;
  workflowDefinitionId: number;
  specKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  owningDepartmentId: number | null;
  defaultResponsibilityId: number | null;
  processAreaLabel: string | null;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  dueInDays: number | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  conditionCount: number;
  dependencyCount: number;
};

export type AdminTaskSpecCondition = {
  id: number;
  taskSpecId: number;
  conditionGroup: number;
  answerKey: string;
  operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
  expectedValueText: string | null;
  expectedValueBoolean: boolean | null;
  expectedValueNumber: number | null;
};

export type AdminTaskSpecDependency = {
  id: number;
  taskSpecId: number;
  dependsOnTaskSpecId: number;
  dependsOnSpecTitle: string;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

export type AdminDependencyGraphNode = {
  id: number;
  title: string;
  category: string;
};

export type AdminDependencyGraphEdge = {
  id: number;
  sourceSpecId: number;
  targetSpecId: number;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

export type AdminDependencyGraph = {
  nodes: AdminDependencyGraphNode[];
  edges: AdminDependencyGraphEdge[];
};

export type AdminAnswerDefinition = {
  id: number;
  workflowDefinitionId: number;
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select" | "person_lookup";
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
};

export type AdminRoleAnswerDefault = {
  workflowDefinitionId: number;
  appRoleId: number;
  answerKey: string;
  defaultValueText: string | null;
  defaultValueBoolean: boolean | null;
};

export type AdminWorkflowValidationIssue = {
  code: string;
  severity: string;
  scope: string;
  message: string;
  referenceKey: string | null;
};

export type AdminWorkflowNodeAction = {
  actionKey: string | null;
  inputMapping: Record<string, unknown> | null;
  executionOrder: number;
  onErrorBehavior: string | null;
};

export type AdminWorkflowDefinitionNode = {
  nodeKey: string | null;
  nodeType: string | null;
  title: string | null;
  sortOrder: number;
  positionX: number | null;
  positionY: number | null;
  config: Record<string, unknown> | null;
  actions: AdminWorkflowNodeAction[];
  // FE-9: Task-Specs reisen mit der Version. Bei measure_*-Nodes 0..N, bei
  // task/approval 0..1. Builder editiert sie (noch) nicht inline, muss sie
  // aber durch Save round-trippen, sonst werden sie beim Replace geloescht.
  specs: AdminWorkflowDefinitionNodeSpec[];
  // Slice 2 (Admin-Gated-Automation): Approval-Rolle fuer task-Nodes mit
  // Action-Bundle. Whitelist auth_admin/auth_hr/auth_manager; null fuer alle
  // anderen Node-Typen und task-Nodes ohne Actions.
  automationAdminRole: string | null;
};

export type AdminWorkflowDefinitionNodeSpec = {
  specKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  defaultResponsibilityId: number | null;
  processAreaLabel: string | null;
  isDepartmentPhaseTask: boolean;
  isRequired: boolean;
  dueInDays: number | null;
  sortOrder: number;
  conditions: AdminWorkflowDefinitionNodeSpecCondition[];
  dependencies: AdminWorkflowDefinitionNodeSpecDependency[];
};

export type AdminWorkflowDefinitionNodeSpecCondition = {
  answerKey: string;
  operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
  expectedValueText: string | null;
  expectedValueBoolean: boolean | null;
  expectedValueNumber: number | null;
};

export type AdminWorkflowDefinitionNodeSpecDependency = {
  dependsOnSpecKey: string;
};

export type AdminWorkflowDefinitionEdge = {
  sourceNodeKey: string | null;
  targetNodeKey: string | null;
  priority: number;
  conditionExpression: string | null;
};

export type AdminWorkflowDefinitionVersionSummary = {
  id: number;
  workflowDefinitionId: number;
  versionNumber: number;
  status: string;
  name: string | null;
  description: string | null;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  canPublish: boolean;
  validationIssues: AdminWorkflowValidationIssue[];
};

export type AdminWorkflowDefinitionSummary = {
  id: number;
  key: string;
  name: string;
  description: string | null;
  versions: AdminWorkflowDefinitionVersionSummary[];
};

export type AdminWorkflowDefinitionVersionDetail = {
  id: number;
  workflowDefinitionId: number;
  definitionKey: string;
  definitionName: string;
  definitionDescription: string | null;
  versionNumber: number;
  status: string;
  name: string | null;
  description: string | null;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  canPublish: boolean;
  validationIssues: AdminWorkflowValidationIssue[];
  nodes: AdminWorkflowDefinitionNode[];
  edges: AdminWorkflowDefinitionEdge[];
};

export type AdminWorkflowActionDefinition = {
  id: number;
  actionKey: string;
  displayName: string;
  description: string | null;
  handlerKey: string;
  isSimulated: boolean;
  isIdempotent: boolean;
  isActive: boolean;
  requiresApproval: boolean;
  inputSchema: Record<string, unknown> | null;
};
