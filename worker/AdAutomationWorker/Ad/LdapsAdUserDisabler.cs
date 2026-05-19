using System.DirectoryServices.Protocols;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// LDAPS-Implementierung fuer IAdUserDisabler. Deaktiviert einen AD-User via ModifyRequest auf
// userAccountControl (setzt ACCOUNTDISABLE-Bit 0x2).
//
// Ablauf:
//   1. SearchRequest (Scope=Base) auf den uebergebenen DN, Attribut: userAccountControl.
//      Kein Eintrag -> NotFound.
//   2. Lese userAccountControl; wenn Bit 0x2 gesetzt -> AlreadyDisabled (idempotent).
//   3. ModifyRequest: Replace userAccountControl mit aktuellem Wert | 0x2.
//   4. Error-Mapping: Whitelist 49/50/21/19 -> Permanent. Code 32 (NoSuchObject) -> NotFound.
//      Alles andere -> Transient.
internal sealed class LdapsAdUserDisabler : IAdUserDisabler
{
    private const int LdapPort = 636;
    private const int AccountDisableBit = 0x2;
    private const int NoSuchObjectCode = 32;

    private static readonly HashSet<int> PermanentLdapCodes = new()
    {
        49, // InvalidCredentials
        50, // InsufficientAccessRights
        21, // InvalidAttributeSyntax
        19, // ConstraintViolation
    };

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdUserDisabler> logger;

    public LdapsAdUserDisabler(IOptions<WorkerSettings> options, ILogger<LdapsAdUserDisabler> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<(bool Exists, bool IsDisabled)> GetUserStatusAsync(
        string distinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            throw new InvalidOperationException("Worker AdSettings.DcHost is not configured.");

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        var (exists, uac) = ReadUserAccountControl(connection, distinguishedName);
        if (!exists) return Task.FromResult((false, false));
        var isDisabled = (uac & AccountDisableBit) != 0;
        return Task.FromResult((true, isDisabled));
    }

    public Task<AdDisableOutcome> DisableUserAsync(string distinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            return Task.FromResult<AdDisableOutcome>(
                new AdDisableOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();

            var (exists, uac) = ReadUserAccountControl(connection, distinguishedName);
            if (!exists)
            {
                logger.LogWarning("AD user not found at DN '{Dn}' — cannot disable.", distinguishedName);
                return Task.FromResult<AdDisableOutcome>(new AdDisableOutcome.NotFound(distinguishedName));
            }

            if ((uac & AccountDisableBit) != 0)
            {
                logger.LogInformation("AD user at '{Dn}' is already disabled (idempotent).", distinguishedName);
                return Task.FromResult<AdDisableOutcome>(new AdDisableOutcome.AlreadyDisabled(distinguishedName));
            }

            var newUac = uac | AccountDisableBit;
            var modify = new ModifyRequest(
                distinguishedName,
                new DirectoryAttributeModification
                {
                    Name = "userAccountControl",
                    Operation = DirectoryAttributeOperation.Replace,
                });
            modify.Modifications[0].Add(newUac.ToString());
            connection.SendRequest(modify);

            logger.LogInformation("AD user at '{Dn}' disabled (userAccountControl={Uac}).", distinguishedName, newUac);
            return Task.FromResult<AdDisableOutcome>(new AdDisableOutcome.Disabled(distinguishedName));
        }
        catch (DirectoryOperationException ex)
        {
            return Task.FromResult(MapDirectoryException(ex, distinguishedName));
        }
        catch (LdapException ex)
        {
            return Task.FromResult(MapLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdDisableOutcome>(new AdDisableOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error talking to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdDisableOutcome>(new AdDisableOutcome.TransientFailure("Network IO error: " + ex.Message, null));
        }
    }

    private (bool Exists, int UserAccountControl) ReadUserAccountControl(LdapConnection connection, string dn)
    {
        var search = new SearchRequest(
            dn,
            "(objectClass=user)",
            SearchScope.Base,
            "userAccountControl");

        SearchResponse response;
        try
        {
            response = (SearchResponse)connection.SendRequest(search);
        }
        catch (DirectoryOperationException ex) when (IsNoSuchObject(ex))
        {
            return (false, 0);
        }

        if (response.Entries.Count == 0) return (false, 0);

        var entry = response.Entries[0];
        if (!entry.Attributes.Contains("userAccountControl"))
            return (true, 0);

        var uacValue = entry.Attributes["userAccountControl"][0]?.ToString();
        if (!int.TryParse(uacValue, out var uac)) return (true, 0);
        return (true, uac);
    }

    private LdapConnection OpenConnection()
    {
        var identifier = new LdapDirectoryIdentifier(settings.DcHost, LdapPort, fullyQualifiedDnsHostName: true, connectionless: false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Negotiate,
            Timeout = TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds),
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;
        connection.Bind();
        return connection;
    }

    private static AdDisableOutcome MapDirectoryException(DirectoryOperationException ex, string dn)
    {
        var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
        if (code == NoSuchObjectCode) return new AdDisableOutcome.NotFound(dn);
        return MapResultCode(code, ex.Message);
    }

    private static AdDisableOutcome MapLdapException(LdapException ex)
        => MapResultCode(ex.ErrorCode, ex.Message);

    private static AdDisableOutcome MapResultCode(int code, string message)
    {
        return PermanentLdapCodes.Contains(code)
            ? new AdDisableOutcome.PermanentFailure(message, code >= 0 ? code : null)
            : new AdDisableOutcome.TransientFailure(message, code >= 0 ? code : null);
    }

    private static bool IsNoSuchObject(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == NoSuchObjectCode;
}
