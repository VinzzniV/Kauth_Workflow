using Xunit;

namespace API.Tests;

public sealed class AuthorizationPolicyServiceTests
{
    private readonly AuthorizationPolicyService _sut = new();

    // --- HasAnyRole ---

    [Fact]
    public void HasAnyRole_ReturnsFalse_WhenNoRolesProvided()
    {
        var user = CreateUser(AuthorizationRoles.Admin);
        Assert.False(_sut.HasAnyRole(user));
    }

    [Fact]
    public void HasAnyRole_ReturnsFalse_WhenUserHasNoMatchingRole()
    {
        var user = CreateUser(AuthorizationRoles.Hr);
        Assert.False(_sut.HasAnyRole(user, AuthorizationRoles.Admin));
    }

    [Fact]
    public void HasAnyRole_ReturnsTrue_WhenUserHasMatchingRole()
    {
        var user = CreateUser(AuthorizationRoles.Admin);
        Assert.True(_sut.HasAnyRole(user, AuthorizationRoles.Admin));
    }

    [Fact]
    public void HasAnyRole_IsCaseInsensitive()
    {
        var user = CreateUser("AUTH_ADMIN");
        Assert.True(_sut.HasAnyRole(user, AuthorizationRoles.Admin));
    }

    // --- CanAccessWorkflowOverview ---

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Reader)]
    [InlineData(AuthorizationRoles.Manager)]
    public void CanAccessWorkflowOverview_ReturnsTrue_ForAllowedRoles(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanAccessWorkflowOverview(user));
    }

    [Fact]
    public void CanAccessWorkflowOverview_ReturnsFalse_ForWorker()
    {
        var user = CreateUser(AuthorizationRoles.Worker);
        Assert.False(_sut.CanAccessWorkflowOverview(user));
    }

    // --- CanReadWorkflow ---

    [Theory]
    [InlineData(AuthorizationRoles.Hr, WorkflowStatusRules.Draft)]
    [InlineData(AuthorizationRoles.Admin, WorkflowStatusRules.Draft)]
    [InlineData(AuthorizationRoles.Hr, WorkflowStatusRules.WaitingForSupervisor)]
    [InlineData(AuthorizationRoles.Admin, WorkflowStatusRules.Completed)]
    public void CanReadWorkflow_ReturnsTrue_ForHrAndAdmin_ForAnyStatus(string role, string status)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanReadWorkflow(user, status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanReadWorkflow_ReturnsTrue_ForReader_WhenTerminal(string status)
    {
        var user = CreateUser(AuthorizationRoles.Reader);
        Assert.True(_sut.CanReadWorkflow(user, status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.WaitingForSupervisor)]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    public void CanReadWorkflow_ReturnsFalse_ForReader_WhenNotTerminal(string status)
    {
        var user = CreateUser(AuthorizationRoles.Reader);
        Assert.False(_sut.CanReadWorkflow(user, status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanReadWorkflow_ReturnsFalse_ForWorker(string status)
    {
        var user = CreateUser(AuthorizationRoles.Worker);
        Assert.False(_sut.CanReadWorkflow(user, status));
    }

    // --- CanRegularlyEditWorkflow ---

    [Fact]
    public void CanRegularlyEditWorkflow_Hr_CanEdit_Draft()
    {
        var user = CreateUser(AuthorizationRoles.Hr);
        Assert.True(_sut.CanRegularlyEditWorkflow(user, WorkflowStatusRules.Draft));
    }

    [Fact]
    public void CanRegularlyEditWorkflow_Manager_CanEdit_WaitingForSupervisor()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        Assert.True(_sut.CanRegularlyEditWorkflow(user, WorkflowStatusRules.WaitingForSupervisor));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    public void CanRegularlyEditWorkflow_Worker_CanEdit_DepartmentPhase(string status)
    {
        var user = CreateUser(AuthorizationRoles.Worker);
        Assert.True(_sut.CanRegularlyEditWorkflow(user, status));
    }

    [Fact]
    public void CanRegularlyEditWorkflow_ReturnsFalse_WhenRoleDoesNotMatchPhase()
    {
        var hr = CreateUser(AuthorizationRoles.Hr);
        var manager = CreateUser(AuthorizationRoles.Manager);
        var worker = CreateUser(AuthorizationRoles.Worker);

        Assert.False(_sut.CanRegularlyEditWorkflow(hr, WorkflowStatusRules.WaitingForSupervisor));
        Assert.False(_sut.CanRegularlyEditWorkflow(manager, WorkflowStatusRules.Draft));
        Assert.False(_sut.CanRegularlyEditWorkflow(worker, WorkflowStatusRules.Draft));
        Assert.False(_sut.CanRegularlyEditWorkflow(worker, WorkflowStatusRules.WaitingForSupervisor));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanRegularlyEditWorkflow_ReturnsFalse_ForNonEditableEndStatus(string status)
    {
        var worker = CreateUser(AuthorizationRoles.Worker);
        Assert.False(_sut.CanRegularlyEditWorkflow(worker, status));
    }

    [Fact]
    public void CanRegularlyEditWorkflow_Admin_ReturnsFalse_ForAllPhases()
    {
        // Admin does not have a regular edit phase — override paths are used instead.
        var admin = CreateUser(AuthorizationRoles.Admin);
        Assert.False(_sut.CanRegularlyEditWorkflow(admin, WorkflowStatusRules.Draft));
        Assert.False(_sut.CanRegularlyEditWorkflow(admin, WorkflowStatusRules.WaitingForSupervisor));
        Assert.False(_sut.CanRegularlyEditWorkflow(admin, WorkflowStatusRules.InProgress));
    }

    // --- CanCreateWorkflow ---

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Admin)]
    public void CanCreateWorkflow_ReturnsTrue_ForHrManagerAndAdmin(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanCreateWorkflow(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanCreateWorkflow_ReturnsFalse_ForOtherRoles(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanCreateWorkflow(user));
    }

    // --- CanCreateWorkflowForProcessType ---

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Admin)]
    public void CanCreateWorkflowForProcessType_ReturnsTrue_ForHrAndAdmin_RegardlessOfProcessFlag(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: false));
        Assert.True(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: true));
    }

    [Fact]
    public void CanCreateWorkflowForProcessType_ManagerDependsOnProcessFlag()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        Assert.False(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: false));
        Assert.True(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: true));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanCreateWorkflowForProcessType_ReturnsFalse_ForRolesWithoutCreatePermission(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: false));
        Assert.False(_sut.CanCreateWorkflowForProcessType(user, managerCreatableProcessType: true));
    }

    // --- CanCreateOrStartWorkflow ---

    [Fact]
    public void CanCreateOrStartWorkflow_Hr_ReturnsTrue()
    {
        var user = CreateUser(AuthorizationRoles.Hr);
        Assert.True(_sut.CanCreateOrStartWorkflow(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanCreateOrStartWorkflow_ReturnsFalse_ForNonHr(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanCreateOrStartWorkflow(user));
    }

    // --- CanEditSupervisorRequirements ---

    [Theory]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Admin)]
    public void CanEditSupervisorRequirements_ReturnsTrue_ForManagerAndAdmin(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanEditSupervisorRequirements(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanEditSupervisorRequirements_ReturnsFalse_ForOtherRoles(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanEditSupervisorRequirements(user));
    }

    // --- CanAccessSupervisorStep ---

    [Fact]
    public void CanAccessSupervisorStep_Manager_ReturnsTrue()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        Assert.True(_sut.CanAccessSupervisorStep(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Worker)]
    public void CanAccessSupervisorStep_ReturnsFalse_ForNonManager(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanAccessSupervisorStep(user));
    }

    // --- CanAccessTechnicalTasks ---

    [Fact]
    public void CanAccessTechnicalTasks_Worker_ReturnsTrue()
    {
        var user = CreateUser(AuthorizationRoles.Worker);
        Assert.True(_sut.CanAccessTechnicalTasks(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    public void CanAccessTechnicalTasks_ReturnsFalse_ForNonWorker(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanAccessTechnicalTasks(user));
    }

    // --- CanAccessTaskStatusUpdates ---

    [Theory]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Admin)]
    public void CanAccessTaskStatusUpdates_ReturnsTrue_ForWorkerAndAdmin(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanAccessTaskStatusUpdates(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanAccessTaskStatusUpdates_ReturnsFalse_ForOtherRoles(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanAccessTaskStatusUpdates(user));
    }

    // --- CanManageAdminConfiguration ---

    [Fact]
    public void CanManageAdminConfiguration_Admin_ReturnsTrue()
    {
        var user = CreateUser(AuthorizationRoles.Admin);
        Assert.True(_sut.CanManageAdminConfiguration(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanManageAdminConfiguration_ReturnsFalse_ForNonAdmin(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanManageAdminConfiguration(user));
    }

    // --- CanViewTaskAssigneeIdentity ---

    [Fact]
    public void CanViewTaskAssigneeIdentity_Admin_ReturnsTrue()
    {
        var user = CreateUser(AuthorizationRoles.Admin);
        Assert.True(_sut.CanViewTaskAssigneeIdentity(user));
    }

    [Theory]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    public void CanViewTaskAssigneeIdentity_ReturnsFalse_ForNonAdmin(string role)
    {
        var user = CreateUser(role);
        Assert.False(_sut.CanViewTaskAssigneeIdentity(user));
    }

    // --- CanObserveWorkflow ---

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Hr)]
    public void CanObserveWorkflow_ReturnsTrue_ForAdminAndHr_RegardlessOfDepartment(string role)
    {
        var user = CreateUser(role);
        Assert.True(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Draft, observableDepartmentIds: null));
    }

    [Fact]
    public void CanObserveWorkflow_Manager_ReturnsTrue_WhenDepartmentInObservableSet()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        var observableDepts = new HashSet<int> { 10, 20 };
        Assert.True(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Draft, observableDepartmentIds: observableDepts));
    }

    [Fact]
    public void CanObserveWorkflow_Manager_ReturnsFalse_WhenDepartmentNotInObservableSet()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        var observableDepts = new HashSet<int> { 20, 30 };
        Assert.False(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Draft, observableDepartmentIds: observableDepts));
    }

    [Fact]
    public void CanObserveWorkflow_Manager_ReturnsFalse_WhenObservableSetIsNull()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        Assert.False(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Draft, observableDepartmentIds: null));
    }

    [Fact]
    public void CanObserveWorkflow_Reader_ReturnsTrue_ForTerminalWorkflow()
    {
        var user = CreateUser(AuthorizationRoles.Reader);
        Assert.True(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Completed, observableDepartmentIds: null));
    }

    [Fact]
    public void CanObserveWorkflow_Reader_ReturnsFalse_ForActiveWorkflow()
    {
        var user = CreateUser(AuthorizationRoles.Reader);
        Assert.False(_sut.CanObserveWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.Draft, observableDepartmentIds: null));
    }

    // --- CanAccessAssignedSupervisorWorkflow ---

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanAccessAssignedSupervisorWorkflow_ReturnsFalse_WhenStatusIsNotWaitingForSupervisor(string status)
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var assignedDepts = new HashSet<int> { 10 };
        Assert.False(_sut.CanAccessAssignedSupervisorWorkflow(admin, workflowDepartmentId: 10, status, assignedDepartmentIds: assignedDepts));
    }

    [Fact]
    public void CanAccessAssignedSupervisorWorkflow_Admin_ReturnsTrue()
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        Assert.True(_sut.CanAccessAssignedSupervisorWorkflow(admin, workflowDepartmentId: 10, WorkflowStatusRules.WaitingForSupervisor, assignedDepartmentIds: new HashSet<int>()));
    }

    [Fact]
    public void CanAccessAssignedSupervisorWorkflow_Manager_ReturnsTrue_WhenDepartmentAssigned()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        var assignedDepts = new HashSet<int> { 10, 20 };
        Assert.True(_sut.CanAccessAssignedSupervisorWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.WaitingForSupervisor, assignedDepartmentIds: assignedDepts));
    }

    [Fact]
    public void CanAccessAssignedSupervisorWorkflow_Manager_ReturnsFalse_WhenDepartmentNotAssigned()
    {
        var user = CreateUser(AuthorizationRoles.Manager);
        var assignedDepts = new HashSet<int> { 20 };
        Assert.False(_sut.CanAccessAssignedSupervisorWorkflow(user, workflowDepartmentId: 10, WorkflowStatusRules.WaitingForSupervisor, assignedDepartmentIds: assignedDepts));
    }

    // --- CanUpdateTaskStatus ---

    [Theory]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanUpdateTaskStatus_ReturnsFalse_ForNonEditableEndStatus(string workflowStatus)
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var task = CreateTaskWithResponsibilityAssignment("some_task", workflowStatus, responsibilityId: 1);
        Assert.False(_sut.CanUpdateTaskStatus(admin, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_ReturnsFalse_ForApprovalTask()
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var task = CreateTaskWithResponsibilityAssignment(
            "department_approval_custom",
            WorkflowStatusRules.WaitingForSupervisor,
            responsibilityId: 1,
            isApprovalTask: true);
        Assert.False(_sut.CanUpdateTaskStatus(admin, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Admin_ReturnsTrue_ForNonTerminalWorkflow()
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var task = CreateTaskWithResponsibilityAssignment("hardware_procure", WorkflowStatusRules.InProgress, responsibilityId: 1);
        Assert.True(_sut.CanUpdateTaskStatus(admin, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsTrue_WithMatchingResponsibilityAssignment()
    {
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 5);
        Assert.True(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsFalse_WithNonMatchingResponsibilityAssignment()
    {
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 99);
        Assert.False(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsTrue_WithMatchingUserAssignment()
    {
        var user = CreateUser(AuthorizationRoles.Worker, userId: 42);
        var task = CreateTaskWithUserAssignment("hardware_setup", WorkflowStatusRules.InProgress, assigneeUserId: 42);
        Assert.True(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsFalse_WithDifferentUserAssignment()
    {
        var user = CreateUser(AuthorizationRoles.Worker, userId: 42);
        var task = CreateTaskWithUserAssignment("hardware_setup", WorkflowStatusRules.InProgress, assigneeUserId: 99);
        Assert.False(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_UserAssignment_DoesNotLeakThroughResponsibility()
    {
        // A worker with a responsibility must NOT get access to a user-assigned task
        // even if they happen to share department coverage. (Decision 4: assignment_type strict enforcement)
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithUserAssignment("hardware_setup", WorkflowStatusRules.InProgress, assigneeUserId: 999);
        Assert.False(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsFalse_WhenNoPrimaryAssignment()
    {
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithNoAssignment("hardware_setup", WorkflowStatusRules.InProgress);
        Assert.False(_sut.CanUpdateTaskStatus(user, task));
    }

    [Fact]
    public void CanUpdateTaskStatus_Worker_ReturnsFalse_InSupervisorPhase()
    {
        // Worker is not in the right phase for waiting_for_supervisor
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithResponsibilityAssignment("some_task", WorkflowStatusRules.WaitingForSupervisor, responsibilityId: 5);
        Assert.False(_sut.CanUpdateTaskStatus(user, task));
    }

    // --- CanUpdateTaskAssignment ---

    [Fact]
    public void CanUpdateTaskAssignment_Admin_ReturnsTrue_ForNonTerminalWorkflow()
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 1);
        Assert.True(_sut.CanUpdateTaskAssignment(admin, task));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Completed)]
    public void CanUpdateTaskAssignment_Admin_ReturnsFalse_ForNonEditableEndStatus(string workflowStatus)
    {
        var admin = CreateUser(AuthorizationRoles.Admin);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", workflowStatus, responsibilityId: 1);
        Assert.False(_sut.CanUpdateTaskAssignment(admin, task));
    }

    [Fact]
    public void CanUpdateTaskAssignment_Worker_ReturnsFalse()
    {
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 5);
        Assert.False(_sut.CanUpdateTaskAssignment(user, task));
    }

    // --- CanAddTaskComment ---

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Hr)]
    [InlineData(AuthorizationRoles.Manager)]
    public void CanAddTaskComment_ReturnsTrue_ForAdminHrAndManager(string roleKey)
    {
        var user = CreateUser(roleKey);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 5);

        Assert.True(_sut.CanAddTaskComment(user, task));
    }

    [Fact]
    public void CanAddTaskComment_Worker_ReturnsTrue_WithMatchingAssignment()
    {
        var user = CreateUserWithResponsibility(AuthorizationRoles.Worker, responsibilityId: 5);
        var task = CreateTaskWithResponsibilityAssignment("hardware_setup", WorkflowStatusRules.InProgress, responsibilityId: 5);

        Assert.True(_sut.CanAddTaskComment(user, task));
    }

    [Fact]
    public void CanAddTaskComment_ReturnsFalse_ForTerminalTask()
    {
        var user = CreateUser(AuthorizationRoles.Hr);
        var task = CreateTaskWithWorkflow(
            "hardware_setup",
            WorkflowStatusRules.InProgress,
            [],
            taskStatus: "done");

        Assert.False(_sut.CanAddTaskComment(user, task));
    }

    // --- Helpers ---

    private static CurrentUser CreateUser(string roleKey, long userId = 1)
    {
        var role = CreateRole(roleKey);
        return new CurrentUser
        {
            UserId = userId,
            DisplayName = "Test User",
            Email = "test@example.com",
            IsActive = true,
            IdentityProvider = "demo",
            Groups = [],
            DirectRoles = [role],
            GroupRoles = [],
            EffectiveRoles = [role],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }

    private static CurrentUser CreateUserWithResponsibility(string roleKey, int responsibilityId, long userId = 1)
    {
        var role = CreateRole(roleKey);
        var responsibility = new CurrentUserResponsibility
        {
            ResponsibilityId = responsibilityId,
            ResponsibilityKey = $"resp_{responsibilityId}",
            ResponsibilityName = $"Responsibility {responsibilityId}",
            ResponsibilityType = "department",
            AssignmentSource = "direct"
        };
        return new CurrentUser
        {
            UserId = userId,
            DisplayName = "Test User",
            Email = "test@example.com",
            IsActive = true,
            IdentityProvider = "demo",
            Groups = [],
            DirectRoles = [role],
            GroupRoles = [],
            EffectiveRoles = [role],
            DirectResponsibilities = [responsibility],
            GroupResponsibilities = [],
            EffectiveResponsibilities = [responsibility]
        };
    }

    private static CurrentUserRole CreateRole(string roleKey)
    {
        return new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = roleKey,
            RoleName = roleKey,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "direct"
        };
    }

    private static TaskWithWorkflowDto CreateTaskWithResponsibilityAssignment(
        string taskKey,
        string workflowStatus,
        int responsibilityId,
        bool isApprovalTask = false)
    {
        var assignment = new WorkflowTaskAssignmentDto
        {
            Id = 1,
            AssignmentType = "responsibility",
            IsPrimary = true,
            AssigneeUserId = null,
            AssigneeResponsibilityId = responsibilityId,
            AssignedAt = DateTime.UtcNow
        };
        return CreateTaskWithWorkflow(taskKey, workflowStatus, [assignment], isApprovalTask: isApprovalTask);
    }

    private static TaskWithWorkflowDto CreateTaskWithUserAssignment(
        string taskKey,
        string workflowStatus,
        long assigneeUserId,
        bool isApprovalTask = false)
    {
        var assignment = new WorkflowTaskAssignmentDto
        {
            Id = 1,
            AssignmentType = "user",
            IsPrimary = true,
            AssigneeUserId = assigneeUserId,
            AssigneeResponsibilityId = null,
            AssignedAt = DateTime.UtcNow
        };
        return CreateTaskWithWorkflow(taskKey, workflowStatus, [assignment], isApprovalTask: isApprovalTask);
    }

    private static TaskWithWorkflowDto CreateTaskWithNoAssignment(string taskKey, string workflowStatus, bool isApprovalTask = false)
    {
        return CreateTaskWithWorkflow(taskKey, workflowStatus, [], isApprovalTask: isApprovalTask);
    }

    private static TaskWithWorkflowDto CreateTaskWithWorkflow(
        string taskKey,
        string workflowStatus,
        List<WorkflowTaskAssignmentDto> assignments,
        string taskStatus = "ready",
        bool isApprovalTask = false)
    {
        var task = new WorkflowTaskDto
        {
            Id = 1,
            TaskTemplateId = null,
            TaskKey = taskKey,
            IsApprovalTask = isApprovalTask,
            Title = taskKey,
            Description = taskKey,
            Category = "test",
            IconKey = "test",
            Status = taskStatus,
            IsRequired = true,
            DueInDays = 3,
            DueAt = DateTime.UtcNow.AddDays(3),
            SlaStatus = "on_track",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            ReadyAt = null,
            StartedAt = null,
            CompletedAt = null,
            ProcessArea = "IT",
            IsDepartmentPhaseTask = true,
            CanUpdateStatus = false,
            CanAddComment = false,
            Assignments = assignments,
            Dependencies = [],
            Comments = []
        };

        var workflow = new TaskWorkflowContextDto
        {
            WorkflowId = 1,
            WorkflowUid = Guid.NewGuid(),
            WorkflowStatus = workflowStatus,
            WorkflowLegacyStatus = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
            WorkflowCreatedAt = DateTime.UtcNow,
            FirstName = "Max",
            LastName = "Mustermann",
            EmployeeNumber = 1000,
            BadgeNumber = 2000,
            DepartmentId = 10,
            DepartmentName = "IT",
            RoleId = 1,
            RoleName = "Mitarbeiter"
        };

        return new TaskWithWorkflowDto { Task = task, Workflow = workflow };
    }
}
