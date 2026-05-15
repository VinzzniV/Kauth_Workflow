using Xunit;

namespace API.Tests;

public sealed class PostgresWorkflowRepositoryTaskStatusRulesTests
{
    [Fact]
    public void NormalizeTaskStatus_RejectsSkipped()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TaskStatusRules.NormalizeTaskStatus("skipped"));
        Assert.Equal("Task status 'skipped' is invalid.", exception.Message);
    }

    [Fact]
    public void NormalizeTaskStatus_RejectsCancelled()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TaskStatusRules.NormalizeTaskStatus("cancelled"));
        Assert.Equal("Task status 'cancelled' is invalid.", exception.Message);
    }

    [Fact]
    public void EnsureTaskTransitionAllowed_RejectsBlockedToDone()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TaskStatusRules.EnsureTaskTransitionAllowed("blocked", "done"));
        Assert.Equal("Task transition from 'blocked' to 'done' is not allowed.", exception.Message);
    }

    [Fact]
    public void EnsureTaskTransitionAllowed_AllowsBlockedToReady()
    {
        var exception = Record.Exception(() => TaskStatusRules.EnsureTaskTransitionAllowed("blocked", "ready"));
        Assert.Null(exception);
    }

    [Fact]
    public void CanAutoBlockTask_ReturnsFalseForInProgress()
    {
        var canAutoBlock = TaskStatusRules.CanAutoBlockTask("in_progress");
        Assert.False(canAutoBlock);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("ready")]
    public void CanAutoBlockTask_ReturnsTrueForOpenAndReady(string status)
    {
        var canAutoBlock = TaskStatusRules.CanAutoBlockTask(status);
        Assert.True(canAutoBlock);
    }

    [Fact]
    public void DetermineActiveWorkflowStatus_ReturnsWaitingForSupervisor_WhenProcessRequiresSupervisorStep()
    {
        var status = WorkflowStatusRules.DetermineActiveWorkflowStatus(
            [
                ("supervisor_fills_document", "ready", true),
                ("hardware_setup", "blocked", true)
            ],
            workflowDefinitionName: "Onboarding",
            requiresSupervisorStep: true,
            approvalSpecKey: "supervisor_fills_document");

        Assert.Equal("waiting_for_supervisor", status);
    }

    [Fact]
    public void DetermineActiveWorkflowStatus_SkipsWaitingForSupervisor_WhenProcessDoesNotRequireSupervisorStep()
    {
        var status = WorkflowStatusRules.DetermineActiveWorkflowStatus(
            [
                ("supervisor_fills_document", "ready", true),
                ("hardware_setup", "ready", true)
            ],
            workflowDefinitionName: "Offboarding",
            requiresSupervisorStep: false,
            approvalSpecKey: null);

        Assert.Equal("waiting_for_department", status);
    }

    [Fact]
    public void DetermineActiveWorkflowStatus_UsesConfiguredApprovalTaskKey()
    {
        var status = WorkflowStatusRules.DetermineActiveWorkflowStatus(
            [
                ("department_approval_custom", "in_progress", true),
                ("hardware_setup", "blocked", true)
            ],
            workflowDefinitionName: "Abteilungsfreigabe",
            requiresSupervisorStep: true,
            approvalSpecKey: "department_approval_custom");

        Assert.Equal("waiting_for_supervisor", status);
    }

    [Fact]
    public void DetermineActiveWorkflowStatus_ReturnsInProgress_WhenDepartmentTaskRunsWithoutSupervisorPhase()
    {
        var status = WorkflowStatusRules.DetermineActiveWorkflowStatus(
            [
                ("hardware_setup", "in_progress", true),
                ("hardware_handover", "blocked", true)
            ],
            workflowDefinitionName: "Mutation",
            requiresSupervisorStep: false,
            approvalSpecKey: null);

        Assert.Equal("in_progress", status);
    }
}
