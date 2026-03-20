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
  mode: "enabled" | "disabled";
  tenantId: string | null;
  clientId: string | null;
  senderEmail: string | null;
  frontendBaseUrl: string;
  testRecipientEmail: string | null;
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
