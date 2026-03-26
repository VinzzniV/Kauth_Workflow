// Frontend-Typen fuer Demo-Login, aktuellen Benutzer und Admin-Stammdaten.
export type DemoLoginUserOption = {
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
};

export type DemoLoginResponse = {
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
  isActive: boolean;
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
  departmentId: number | null;
  departmentName: string | null;
  roles: AdminRole[];
  groups: AdminGroupRef[];
};

export type AdminDepartmentAssignment = {
  departmentId: number;
  departmentName: string;
  departmentLeadUserId: number | null;
  departmentLeadDisplayName: string | null;
  requirementOwnerUserId: number | null;
  requirementOwnerDisplayName: string | null;
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

export type AdminNotificationEmailConfiguration = {
  enabled: boolean;
  mode: "enabled" | "disabled" | "sandbox";
  tenantId: string | null;
  clientId: string | null;
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
