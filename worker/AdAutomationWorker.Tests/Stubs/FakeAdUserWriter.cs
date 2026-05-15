using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdUserWriter : IAdUserWriter
{
    public Func<AdUserSpec, AdWriteOutcome> OutcomeFactory { get; set; } = _ =>
        new AdWriteOutcome.Created(DistinguishedName: "CN=Test,OU=Test,DC=test,DC=local");

    public Func<string, (bool Exists, string? Dn)> FindUserFactory { get; set; } = _ =>
        (false, null);

    public List<AdUserSpec> Calls { get; } = new();
    public List<string> FindUserCalls { get; } = new();

    public Task<AdWriteOutcome> CreateUserAsync(AdUserSpec spec, CancellationToken cancellationToken)
    {
        Calls.Add(spec);
        return Task.FromResult(OutcomeFactory(spec));
    }

    public Task<(bool Exists, string? DistinguishedName)> FindUserAsync(
        string samAccountName, CancellationToken cancellationToken)
    {
        FindUserCalls.Add(samAccountName);
        var (exists, dn) = FindUserFactory(samAccountName);
        return Task.FromResult((exists, dn));
    }
}
