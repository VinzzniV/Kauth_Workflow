using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdUserWriter : IAdUserWriter
{
    public Func<AdUserSpec, AdWriteOutcome> OutcomeFactory { get; set; } = _ =>
        new AdWriteOutcome.Created(DistinguishedName: "CN=Test,OU=Test,DC=test,DC=local");

    public List<AdUserSpec> Calls { get; } = new();

    public Task<AdWriteOutcome> CreateUserAsync(AdUserSpec spec, CancellationToken cancellationToken)
    {
        Calls.Add(spec);
        return Task.FromResult(OutcomeFactory(spec));
    }
}
