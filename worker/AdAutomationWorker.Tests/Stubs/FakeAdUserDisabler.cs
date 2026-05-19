using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdUserDisabler : IAdUserDisabler
{
    public Func<string, AdDisableOutcome> OutcomeFactory { get; set; } = dn =>
        new AdDisableOutcome.Disabled(dn);

    public Func<string, (bool Exists, bool IsDisabled)> StatusFactory { get; set; } = _ =>
        (true, false);

    public List<string> DisableCalls { get; } = new();
    public List<string> StatusCalls { get; } = new();

    public Task<AdDisableOutcome> DisableUserAsync(string distinguishedName, CancellationToken cancellationToken)
    {
        DisableCalls.Add(distinguishedName);
        return Task.FromResult(OutcomeFactory(distinguishedName));
    }

    public Task<(bool Exists, bool IsDisabled)> GetUserStatusAsync(string distinguishedName, CancellationToken cancellationToken)
    {
        StatusCalls.Add(distinguishedName);
        return Task.FromResult(StatusFactory(distinguishedName));
    }
}
