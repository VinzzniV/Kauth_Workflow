import type { ReactNode } from "react";
import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { CurrentUserContext } from "../src/auth/useCurrentUser";
import { canAccessFeature, deriveRoleCapabilities, toRoleLabel } from "../src/auth/roleModel";
import type { TaskWithWorkflow, WorkflowRequirementSnapshot, WorkflowSummary, WorkflowTask } from "../src/types/workflow";

type RenderOptions = {
  roleKeys?: string[];
  route?: string;
};

export function renderWithApp(ui: ReactNode, options: RenderOptions = {}) {
  const roleKeys = options.roleKeys ?? ["auth_hr"];
  const capabilities = deriveRoleCapabilities(roleKeys);
  const currentUser = {
    username: "tester",
    displayName: "Test User",
    email: "tester@example.com",
    roles: [...roleKeys],
    groups: [] as string[],
  };

  return render(
    <MemoryRouter initialEntries={[options.route ?? "/"]}>
      <CurrentUserContext.Provider
        value={{
          status: "authenticated",
          currentUser,
          displayName: currentUser.displayName,
          roles: currentUser.roles,
          roleLabels: currentUser.roles.map(toRoleLabel),
          groups: currentUser.groups,
          capabilities,
          defaultRoute: "/",
          canAccessFeature: (feature) => canAccessFeature(capabilities, feature),
          refreshCurrentUser: async () => undefined,
        }}
      >
        {ui}
      </CurrentUserContext.Provider>
    </MemoryRouter>
  );
}

export function createWorkflowSummary(overrides: Partial<WorkflowSummary> = {}): WorkflowSummary {
  return {
    uid: "wf-1",
    processType: {
      key: "onboarding",
      name: "Onboarding",
    },
    firstName: "Alice",
    lastName: "Example",
    employeeNumber: 1001,
    badgeNumber: 2001,
    departmentId: 10,
    departmentName: "IT",
    roleId: 5,
    roleName: "Engineer",
    status: "open",
    workflowStatus: "waiting_for_department",
    createdAt: "2026-03-20T10:00:00.000Z",
    pendingNotifications: 0,
    failedNotifications: 0,
    requirementSummary: {
      totalCount: 3,
      visibleCount: 3,
      answeredVisibleCount: 2,
      pendingVisibleCount: 1,
    },
    taskMetrics: {
      overall: {
        totalCount: 2,
        openCount: 1,
        inProgressCount: 0,
        doneCount: 1,
        completedCount: 1,
        activeCount: 1,
      },
      departmentPhase: {
        totalCount: 2,
        openCount: 1,
        inProgressCount: 0,
        doneCount: 1,
        completedCount: 1,
        activeCount: 1,
      },
    },
    taskSummary: "Offen: 1 | Erledigt: 1",
    responsibilityOptions: [{ value: "it", label: "IT" }],
    ...overrides,
  };
}

export function createTaskWithWorkflow(
  taskOverrides: Partial<WorkflowTask> = {},
  workflowOverrides: Partial<TaskWithWorkflow["workflow"]> = {}
): TaskWithWorkflow {
  return {
    task: {
      id: 1,
      taskTemplateId: null,
      taskKey: "hardware_setup",
      isApprovalTask: false,
      title: "Hardware einrichten",
      description: "Notebook vorbereiten",
      category: "hardware",
      iconKey: "laptop",
      status: "open",
      isRequired: true,
      dueInDays: 3,
      dueAt: "2026-03-23T10:00:00.000Z",
      slaStatus: "on_track",
      sortOrder: 10,
      createdAt: "2026-03-20T10:00:00.000Z",
      readyAt: null,
      startedAt: null,
      completedAt: null,
      processArea: "IT",
      isDepartmentPhaseTask: true,
      canUpdateStatus: false,
      canAddComment: false,
      assignments: [
        {
          id: 1,
          assignmentType: "responsibility",
          isPrimary: true,
          assigneeUserId: null,
          assigneeUserName: null,
          assigneeUserEmail: null,
          assigneeResponsibilityId: 10,
          assigneeResponsibilityKey: "it",
          assigneeResponsibilityName: "IT",
          assigneeResponsibilityType: "department",
          assignedAt: "2026-03-20T10:00:00.000Z",
          completedAt: null,
        },
      ],
      dependencies: [],
      comments: [],
      ...taskOverrides,
    },
    workflow: {
      workflowId: 1,
      workflowUid: "wf-1",
      workflowStatus: "waiting_for_department",
      workflowCreatedAt: "2026-03-20T10:00:00.000Z",
      firstName: "Alice",
      lastName: "Example",
      employeeNumber: 1001,
      badgeNumber: 2001,
      departmentId: 10,
      departmentName: "IT",
      roleId: 5,
      roleName: "Engineer",
      ...workflowOverrides,
    },
  };
}

export function createRequirementSnapshot(
  overrides: Partial<WorkflowRequirementSnapshot> = {}
): WorkflowRequirementSnapshot {
  return {
    workflowRequirementId: 1,
    id: 1,
    key: "ad_user_requested",
    title: "AD-Benutzer anlegen",
    description: "Soll ein AD-Benutzer angelegt werden?",
    category: "accounts",
    iconKey: "user",
    inputType: "boolean",
    isRequired: true,
    isVisible: true,
    sortOrder: 1,
    behavior: {
      visibilityDependencies: [],
      validation: null,
      resetTargetsWhenNotTrue: [],
      singleSelectReset: null,
    },
    options: [],
    value: {
      valueBoolean: null,
      valueText: null,
      valueNumber: null,
      selectedOptionId: null,
      selectedOptionKey: null,
      selectedOptionValue: null,
      selectedOptionLabel: null,
      selectedOptions: [],
    },
    ...overrides,
  };
}
