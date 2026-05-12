using Xunit;

namespace API.Tests;

public sealed class WorkflowStatusRulesTests
{
    // --- IsTerminal ---

    [Theory]
    [InlineData(WorkflowStatusRules.Completed)]
    [InlineData("COMPLETED")]
    [InlineData("  completed  ")]
    [InlineData(WorkflowStatusRules.Cancelled)]
    [InlineData("CANCELLED")]
    public void IsTerminal_ReturnsTrue_ForTerminalStatuses(string status)
    {
        Assert.True(WorkflowStatusRules.IsTerminal(status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.WaitingForSupervisor)]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    public void IsTerminal_ReturnsFalse_ForActiveStatuses(string status)
    {
        Assert.False(WorkflowStatusRules.IsTerminal(status));
    }

    // --- IsWaitingForSupervisor ---

    [Fact]
    public void IsWaitingForSupervisor_ReturnsTrue_ForWaitingForSupervisor()
    {
        Assert.True(WorkflowStatusRules.IsWaitingForSupervisor(WorkflowStatusRules.WaitingForSupervisor));
        Assert.True(WorkflowStatusRules.IsWaitingForSupervisor("WAITING_FOR_SUPERVISOR"));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    [InlineData(WorkflowStatusRules.Completed)]
    public void IsWaitingForSupervisor_ReturnsFalse_ForOtherStatuses(string status)
    {
        Assert.False(WorkflowStatusRules.IsWaitingForSupervisor(status));
    }

    // --- IsDepartmentPhase ---

    [Theory]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData(WorkflowStatusRules.InProgress)]
    public void IsDepartmentPhase_ReturnsTrue_ForDepartmentPhaseStatuses(string status)
    {
        Assert.True(WorkflowStatusRules.IsDepartmentPhase(status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.WaitingForSupervisor)]
    [InlineData(WorkflowStatusRules.Completed)]
    [InlineData("cancelled")]
    public void IsDepartmentPhase_ReturnsFalse_ForNonDepartmentPhaseStatuses(string status)
    {
        Assert.False(WorkflowStatusRules.IsDepartmentPhase(status));
    }

    // --- Normalize ---

    [Theory]
    [InlineData("DRAFT", "draft")]
    [InlineData("  In_Progress  ", "in_progress")]
    [InlineData("Completed", "completed")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, WorkflowStatusRules.Normalize(input));
    }

    // --- IsCancellable ---

    [Theory]
    [InlineData(WorkflowStatusRules.InProgress)]
    [InlineData(WorkflowStatusRules.WaitingForSupervisor)]
    [InlineData(WorkflowStatusRules.WaitingForDepartment)]
    [InlineData("IN_PROGRESS")]
    [InlineData("  waiting_for_supervisor  ")]
    public void IsCancellable_ReturnsTrue_ForActiveSourceStatuses(string status)
    {
        Assert.True(WorkflowStatusRules.IsCancellable(status));
    }

    [Theory]
    [InlineData(WorkflowStatusRules.Draft)]
    [InlineData(WorkflowStatusRules.Completed)]
    [InlineData(WorkflowStatusRules.Cancelled)]
    [InlineData("archived")]
    public void IsCancellable_ReturnsFalse_ForNonActiveStatuses(string status)
    {
        Assert.False(WorkflowStatusRules.IsCancellable(status));
    }
}
