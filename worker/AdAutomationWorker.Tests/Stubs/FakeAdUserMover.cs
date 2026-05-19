using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Tests.Stubs;

internal sealed class FakeAdUserMover : IAdUserMover
{
    public Func<string, string, AdMoveOutcome> OutcomeFactory { get; set; } = (dn, targetOu) =>
        new AdMoveOutcome.Moved($"CN=User,{targetOu}", "OU=Old,DC=example,DC=local", targetOu);

    public Func<string, (bool Exists, string? CurrentOu)> OuFactory { get; set; } = _ =>
        (true, "OU=Current,DC=example,DC=local");

    public List<(string Dn, string TargetOu)> MoveCalls { get; } = new();
    public List<string> OuCalls { get; } = new();

    public Task<AdMoveOutcome> MoveUserAsync(string userDistinguishedName, string targetOu, CancellationToken cancellationToken)
    {
        MoveCalls.Add((userDistinguishedName, targetOu));
        return Task.FromResult(OutcomeFactory(userDistinguishedName, targetOu));
    }

    public Task<(bool Exists, string? CurrentOu)> GetUserOuAsync(string userDistinguishedName, CancellationToken cancellationToken)
    {
        OuCalls.Add(userDistinguishedName);
        return Task.FromResult(OuFactory(userDistinguishedName));
    }
}
