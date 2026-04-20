export type RotationPlanStatus = "draft" | "active" | "completed" | "archived";
export type RotationStationStatus = "planned" | "active" | "completed" | "cancelled";
export type RotationGeneratedTaskStatus =
  | "open"
  | "in_progress"
  | "completed"
  | "failed"
  | "cancelled";
export type RotationTriggerType = "enter" | "exit";
export type RotationTaskType = "manual" | "technical" | "approval" | "information";
export type RotationNotificationStatus = "pending" | "sent" | "failed" | "disabled";

export type CompletedOnboardingSearchResult = {
  personId: number;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  employeeNumber: number | null;
  badgeNumber: number | null;
  departmentId: number | null;
  departmentName: string | null;
  roleId: number | null;
  roleName: string | null;
  employmentStatus: string | null;
  appUserId: number | null;
  directoryIdentityId: number | null;
  directoryLinkStatus: string | null;
  directoryDisplayName: string | null;
  directoryUserPrincipalName: string | null;
  directoryMail: string | null;
  directoryEmployeeNumber: number | null;
  latestCompletedOnboardingWorkflowUid: string | null;
  latestCompletedOnboardingAt: string | null;
};

export type RotationPlanListItem = {
  id: number;
  personId: number;
  sourceWorkflowUid: string;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  title: string;
  status: RotationPlanStatus;
  createdByUserId: number | null;
  createdAt: string;
  updatedAt: string;
  stationCount: number;
};

export type RotationStation = {
  id: number;
  rotationPlanId: number;
  departmentId: number;
  departmentName: string;
  startDate: string;
  endDate: string;
  orderIndex: number;
  location: string | null;
  notes: string | null;
  status: RotationStationStatus;
  createdAt: string;
  updatedAt: string;
};

export type RotationPlanDetail = {
  id: number;
  personId: number;
  sourceWorkflowUid: string;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  title: string;
  status: RotationPlanStatus;
  createdByUserId: number | null;
  createdAt: string;
  updatedAt: string;
  stations: RotationStation[];
};

export type RotationTaskAssignment = {
  id: number;
  assignmentType: "responsibility" | "user";
  isPrimary: boolean;
  assigneeUserId: number | null;
  assigneeUserName: string | null;
  assigneeUserEmail: string | null;
  assigneeResponsibilityId: number | null;
  assigneeResponsibilityKey: string | null;
  assigneeResponsibilityName: string | null;
  assigneeResponsibilityType: string | null;
  assignedAt: string;
  completedAt: string | null;
};

export type RotationTaskComment = {
  id: number;
  taskId: number;
  authorUserId: number | null;
  authorUserName: string | null;
  commentText: string;
  createdAt: string;
};

export type RotationGeneratedTask = {
  id: number;
  taskRef: string;
  rotationPlanId: number;
  rotationStationId: number | null;
  personId: number;
  departmentId: number;
  departmentName: string | null;
  templateId: number | null;
  templateTitle: string | null;
  triggerType: RotationTriggerType;
  anchorDate: string;
  title: string;
  description: string | null;
  taskType: RotationTaskType;
  responsibilityId: number | null;
  responsibilityName: string | null;
  dueDate: string | null;
  status: RotationGeneratedTaskStatus;
  completionNote: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
  assignments: RotationTaskAssignment[];
  comments: RotationTaskComment[];
};

export type RotationTaskRegenerationResult = {
  created: number;
  updated: number;
  cancelled: number;
  unchanged: number;
};

export type RotationNotificationPayload = {
  dedupeKey?: string;
  recipientName?: string;
  planTitle?: string;
  sourceWorkflowUid?: string;
  personId?: number;
  personDisplayName?: string;
  currentDepartmentName?: string | null;
  nextDepartmentName?: string | null;
  changeDate?: string | null;
  linkPath?: string | null;
  tasks?: Array<{
    generatedTaskId: number;
    taskRef: string;
    title: string;
    status: string;
    dueDate: string | null;
    departmentName: string | null;
  }>;
};

export type RotationNotification = {
  id: number;
  rotationPlanId: number;
  rotationStationId: number | null;
  generatedTaskId: number | null;
  notificationType: string;
  recipientEmail: string;
  recipientName: string | null;
  recipientUserId: number | null;
  subject: string;
  payload: RotationNotificationPayload | null;
  status: RotationNotificationStatus;
  attempts: number;
  lastError: string | null;
  sentAt: string | null;
  createdAt: string;
};

export type RotationAuditEntry = {
  id: number;
  rotationPlanId: number;
  rotationStationId: number | null;
  generatedTaskId: number | null;
  actorUserId: number | null;
  actorUserName: string | null;
  eventType: string;
  oldValue: Record<string, unknown> | null;
  newValue: Record<string, unknown> | null;
  detail: string | null;
  createdAt: string;
};

export type DepartmentActionTemplate = {
  id: number;
  departmentId: number;
  departmentName: string | null;
  triggerType: RotationTriggerType;
  title: string;
  description: string | null;
  taskType: RotationTaskType;
  defaultResponsibilityId: number | null;
  defaultResponsibilityName: string | null;
  dueOffsetDays: number;
  reminderOffsetDays: number | null;
  isAutomatable: boolean;
  automationKey: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
};

export type DepartmentActionTemplateUpsertPayload = {
  departmentId: number;
  triggerType: RotationTriggerType;
  title: string;
  description?: string | null;
  taskType: RotationTaskType;
  defaultResponsibilityId?: number | null;
  dueOffsetDays: number;
  reminderOffsetDays?: number | null;
  isAutomatable?: boolean;
  automationKey?: string | null;
  isActive?: boolean;
};

export type CreateRotationPlanPayload = {
  personId: number;
  sourceWorkflowUid: string;
  title?: string | null;
  status?: RotationPlanStatus | null;
};

export type RotationStationUpsertPayload = {
  departmentId: number;
  startDate: string;
  endDate: string;
  orderIndex: number;
  location?: string | null;
  notes?: string | null;
  status?: RotationStationStatus | null;
};
