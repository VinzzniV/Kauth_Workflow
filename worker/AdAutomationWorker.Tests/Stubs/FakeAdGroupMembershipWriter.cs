using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdGroupMembershipWriter : IAdGroupMembershipWriter
{
    public Func<AdGroupMembershipSpec, AdGroupMembershipOutcome> OutcomeFactory { get; set; } = spec =>
        new AdGroupMembershipOutcome.AllAdded(
            NewlyAddedGroups: spec.GroupDistinguishedNames.ToArray(),
            AlreadyMemberGroups: Array.Empty<string>());

    public Func<string, string, bool> IsMemberFactory { get; set; } = (_, _) => false;

    public List<AdGroupMembershipSpec> Calls { get; } = new();
    public List<(string UserDn, string GroupDn)> IsMemberCalls { get; } = new();

    public Task<AdGroupMembershipOutcome> AddMembershipsAsync(AdGroupMembershipSpec spec, CancellationToken cancellationToken)
    {
        Calls.Add(spec);
        return Task.FromResult(OutcomeFactory(spec));
    }

    public Task<bool> IsMemberAsync(
        string userDistinguishedName, string groupDistinguishedName, CancellationToken cancellationToken)
    {
        IsMemberCalls.Add((userDistinguishedName, groupDistinguishedName));
        return Task.FromResult(IsMemberFactory(userDistinguishedName, groupDistinguishedName));
    }
}
