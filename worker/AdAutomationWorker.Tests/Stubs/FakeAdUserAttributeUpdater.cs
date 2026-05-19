using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdUserAttributeUpdater : IAdUserAttributeUpdater
{
    public Func<AdUserAttributeUpdateSpec, AdUpdateAttributesOutcome> OutcomeFactory { get; set; } = spec =>
        new AdUpdateAttributesOutcome.Updated(spec.DistinguishedName, spec.Attributes.Keys.ToList());

    public Func<string, (bool Exists, AdUserAttributeSnapshot? Snapshot)> SnapshotFactory { get; set; } = _ =>
        (true, new AdUserAttributeSnapshot());

    public List<AdUserAttributeUpdateSpec> UpdateCalls { get; } = new();
    public List<string> SnapshotCalls { get; } = new();

    public Task<AdUpdateAttributesOutcome> UpdateAttributesAsync(AdUserAttributeUpdateSpec spec, CancellationToken cancellationToken)
    {
        UpdateCalls.Add(spec);
        return Task.FromResult(OutcomeFactory(spec));
    }

    public Task<(bool Exists, AdUserAttributeSnapshot? Snapshot)> GetUserAttributesAsync(string distinguishedName, CancellationToken cancellationToken)
    {
        SnapshotCalls.Add(distinguishedName);
        return Task.FromResult(SnapshotFactory(distinguishedName));
    }
}
