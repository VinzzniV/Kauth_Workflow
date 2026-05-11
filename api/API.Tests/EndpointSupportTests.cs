using Xunit;

namespace API.Tests;

public sealed class WorkflowVisibilityServiceTests
{
    private readonly AuthorizationPolicyService _authorizationPolicy = new();

    [Fact]
    public void ApplyWorkflowTaskPermissions_ClearsComments_ForReaderOnlyUser()
    {
        var sut = new WorkflowVisibilityService(null!, _authorizationPolicy);
        var workflow = CreateWorkflowDetailWithComments();
        var reader = CreateUser(AuthorizationRoles.Reader);

        sut.ApplyWorkflowTaskPermissions(workflow, reader);

        Assert.Empty(workflow.Tasks[0].Comments);
    }

    [Fact]
    public void ApplyWorkflowTaskPermissions_KeepsComments_ForHrUser()
    {
        var sut = new WorkflowVisibilityService(null!, _authorizationPolicy);
        var workflow = CreateWorkflowDetailWithComments();
        var hr = CreateUser(AuthorizationRoles.Hr);

        sut.ApplyWorkflowTaskPermissions(workflow, hr);

        Assert.Single(workflow.Tasks[0].Comments);
    }

    private static WorkflowDetailDto CreateWorkflowDetailWithComments()
    {
        return new WorkflowDetailDto
        {
            Uid = Guid.NewGuid(),
            FirstName = "Max",
            LastName = "Mustermann",
            EmployeeNumber = 1000,
            BadgeNumber = 2000,
            DepartmentId = 10,
            DepartmentName = "IT",
            WorkflowDefinition = new WorkflowDefinitionRefDto
            {
                Key = "onboarding",
                Name = "Onboarding",
                RequiresTargetPerson = false
            },
            RoleId = 1,
            RoleName = "Mitarbeiter",
            WorkflowStatus = WorkflowStatusRules.Completed,
            CreatedAt = DateTime.UtcNow,
            Requirements = [],
            RequirementSummary = WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
            Tasks =
            [
                new WorkflowTaskDto
                {
                    Id = 1,
                    TaskTemplateId = null,
                    TaskKey = "hardware_setup",
                    Title = "Hardware bereitstellen",
                    Description = "Hardware konfigurieren",
                    Category = "IT",
                    IconKey = "pc",
                    Status = "done",
                    IsRequired = true,
                    DueInDays = 3,
                    DueAt = DateTime.UtcNow.AddDays(3),
                    SlaStatus = "none",
                    SortOrder = 10,
                    CreatedAt = DateTime.UtcNow,
                    ReadyAt = DateTime.UtcNow,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    ProcessArea = "IT",
                    IsDepartmentPhaseTask = true,
                    CanUpdateStatus = false,
                    CanAddComment = false,
                    Assignments =
                    [
                        new WorkflowTaskAssignmentDto
                        {
                            Id = 1,
                            AssignmentType = "user",
                            IsPrimary = true,
                            AssigneeUserId = 42,
                            AssigneeUserName = "Jane Doe",
                            AssigneeUserEmail = "jane@example.com",
                            AssigneeResponsibilityId = null,
                            AssigneeResponsibilityKey = null,
                            AssigneeResponsibilityName = null,
                            AssigneeResponsibilityType = null,
                            AssignedAt = DateTime.UtcNow,
                            CompletedAt = null
                        }
                    ],
                    Dependencies = [],
                    Comments =
                    [
                        new WorkflowTaskCommentDto
                        {
                            Id = 1,
                            TaskId = 1,
                            AuthorUserId = 99,
                            AuthorUserName = "Internal User",
                            CommentText = "Nur intern sichtbar.",
                            CreatedAt = DateTime.UtcNow
                        }
                    ]
                }
            ],
            TaskMetrics = WorkflowSummaryBuilder.CreateEmptyTaskMetrics(),
            TaskAreas = [],
            Notifications = []
        };
    }

    private static CurrentUser CreateUser(string roleKey)
    {
        var role = new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = roleKey,
            RoleName = roleKey,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "direct"
        };

        return new CurrentUser
        {
            UserId = 1,
            DisplayName = "Test User",
            Email = "test@example.com",
            IsActive = true,
            IdentityProvider = "dev-sim",
            Groups = [],
            DirectRoles = [role],
            GroupRoles = [],
            EffectiveRoles = [role],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }
}
