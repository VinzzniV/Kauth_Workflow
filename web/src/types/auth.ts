// Frontend-Typen fuer Dev-Simulations-Login, aktuellen Benutzer und Admin-Stammdaten.
export type SimulationLoginUserOption = {
  userId: number;
  username: string;
  displayName: string;
  email: string;
  departmentName: string | null;
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

export type AdminProcessType = {
  id: number;
  key: string;
  name: string;
  description: string | null;
  requiresSupervisorStep: boolean;
  approvalTaskTemplateKey: string | null;
  requiresTargetPerson: boolean;
  iconKey: string | null;
  isActive: boolean;
  sortOrder: number;
  workflowCount: number;
  answerDefinitionCount: number;
  taskTemplateCount: number;
  canActivate: boolean;
  activationBlockedReason: string | null;
};

export type AdminTaskTemplate = {
  id: number;
  processTypeId: number;
  templateKey: string;
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

export type AdminTaskTemplateCondition = {
  id: number;
  taskTemplateId: number;
  conditionGroup: number;
  answerKey: string;
  operator: "eq" | "neq" | "is_true" | "is_false" | "is_null" | "is_not_null";
  expectedValueText: string | null;
  expectedValueBoolean: boolean | null;
  expectedValueNumber: number | null;
};

export type AdminTaskTemplateDependency = {
  id: number;
  taskTemplateId: number;
  dependsOnTaskTemplateId: number;
  dependsOnTemplateTitle: string;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

export type AdminDependencyGraphNode = {
  id: number;
  title: string;
  category: string;
};

export type AdminDependencyGraphEdge = {
  id: number;
  sourceTemplateId: number;
  targetTemplateId: number;
  requiredStatus: "open" | "ready" | "in_progress" | "blocked" | "done";
};

export type AdminDependencyGraph = {
  nodes: AdminDependencyGraphNode[];
  edges: AdminDependencyGraphEdge[];
};

export type AdminAnswerDefinition = {
  id: number;
  processTypeId: number;
  answerKey: string;
  title: string;
  category: string;
  description: string;
  iconKey: string | null;
  inputType: "boolean" | "text" | "select" | "multi_select";
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
};

export type AdminRoleAnswerDefault = {
  processTypeId: number;
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
  inputMapping: unknown | null;
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
  config: unknown | null;
  actions: AdminWorkflowNodeAction[];
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
  primaryLegacyProcessTypeKey: string | null;
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
  primaryLegacyProcessTypeKey: string | null;
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
  isIdempotent: boolean;
  isActive: boolean;
  requiresApproval: boolean;
  inputSchema: unknown | null;
};
