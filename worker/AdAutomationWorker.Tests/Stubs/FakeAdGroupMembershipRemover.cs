using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdGroupMembershipRemover : IAdGroupMembershipRemover
{
    public Func<string, AdRemoveMembershipsOutcome> OutcomeFactory { get; set; } = _ =>
        new AdRemoveMembershipsOutcome.AllRemoved(Array.Empty<string>(), Array.Empty<string>());

    public Func<string, IReadOnlyList<string>> FindGroupsFactory { get; set; } = _ =>
        Array.Empty<string>();

    public List<string> RemoveCalls { get; } = new();
    public List<string> FindGroupsCalls { get; } = new();

    public Task<AdRemoveMembershipsOutcome> RemoveAllMembershipsAsync(
        string userDistinguishedName, CancellationToken cancellationToken)
    {
        RemoveCalls.Add(userDistinguishedName);
        return Task.FromResult(OutcomeFactory(userDistinguishedName));
    }

    public Task<IReadOnlyList<string>> FindMemberOfGroupsAsync(
        string userDistinguishedName, CancellationToken cancellationToken)
    {
        FindGroupsCalls.Add(userDistinguishedName);
        return Task.FromResult(FindGroupsFactory(userDistinguishedName));
    }
}
