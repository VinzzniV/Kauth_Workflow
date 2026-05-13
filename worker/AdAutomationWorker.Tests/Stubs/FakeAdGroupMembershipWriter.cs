using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdGroupMembershipWriter : IAdGroupMembershipWriter
{
    public Func<AdGroupMembershipSpec, AdGroupMembershipOutcome> OutcomeFactory { get; set; } = spec =>
        new AdGroupMembershipOutcome.AllAdded(
            NewlyAddedGroups: spec.GroupDistinguishedNames.ToArray(),
            AlreadyMemberGroups: Array.Empty<string>());

    public List<AdGroupMembershipSpec> Calls { get; } = new();

    public Task<AdGroupMembershipOutcome> AddMembershipsAsync(AdGroupMembershipSpec spec, CancellationToken cancellationToken)
    {
        Calls.Add(spec);
        return Task.FromResult(OutcomeFactory(spec));
    }
}
