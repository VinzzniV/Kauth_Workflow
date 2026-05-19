using System.DirectoryServices.Protocols;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// LDAPS-Implementierung für IAdUserMover. Verschiebt einen AD-User per ModifyDNRequest in eine
// andere OU, wobei der RDN (CN) unverändert bleibt.
//
// Ablauf:
//   1. SearchRequest (Scope=Base) auf den übergenen DN, prüft Existenz.
//      Kein Eintrag -> NotFound.
//   2. Extrahiere Parent-OU aus dem DN (alles nach dem ersten Komma).
//      Gleich targetOu (case-insensitive) -> AlreadyInTargetOu.
//   3. ModifyDNRequest: newParentDN=targetOu, newRDN=aktueller RDN, deleteOldRdn=true.
//   4. Error-Mapping: Code 32 (NoSuchObject) -> NotFound/PermanentFailure.
//      Whitelist 49/50/32/21/19 -> Permanent. Alles andere -> Transient.
internal sealed class LdapsAdUserMover : IAdUserMover
{
    private const int LdapPort = 636;
    private const int NoSuchObjectCode = 32;

    private static readonly HashSet<int> PermanentLdapCodes = new()
    {
        49, // InvalidCredentials
        50, // InsufficientAccessRights
        32, // NoSuchObject
        21, // InvalidAttributeSyntax
        19, // ConstraintViolation
    };

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdUserMover> logger;

    public LdapsAdUserMover(IOptions<WorkerSettings> options, ILogger<LdapsAdUserMover> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<(bool Exists, string? CurrentOu)> GetUserOuAsync(
        string userDistinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            throw new InvalidOperationException("Worker AdSettings.DcHost is not configured.");

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        var exists = UserExists(connection, userDistinguishedName);
        if (!exists) return Task.FromResult((false, (string?)null));
        var currentOu = ExtractParentOu(userDistinguishedName);
        return Task.FromResult((true, currentOu));
    }

    public Task<AdMoveOutcome> MoveUserAsync(
        string userDistinguishedName, string targetOu, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            return Task.FromResult<AdMoveOutcome>(
                new AdMoveOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();

            if (!UserExists(connection, userDistinguishedName))
            {
                logger.LogWarning("AD user not found at DN '{Dn}' — cannot move.", userDistinguishedName);
                return Task.FromResult<AdMoveOutcome>(new AdMoveOutcome.NotFound(userDistinguishedName));
            }

            var currentOu = ExtractParentOu(userDistinguishedName);
            if (string.Equals(currentOu, targetOu, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("AD user at '{Dn}' is already in target OU '{Ou}' (idempotent).", userDistinguishedName, targetOu);
                return Task.FromResult<AdMoveOutcome>(new AdMoveOutcome.AlreadyInTargetOu(userDistinguishedName, targetOu));
            }

            var rdn = ExtractRdn(userDistinguishedName);
            var modifyDn = new ModifyDNRequest(userDistinguishedName, targetOu, rdn)
            {
                DeleteOldRdn = true,
            };
            connection.SendRequest(modifyDn);

            var newDn = $"{rdn},{targetOu}";
            logger.LogInformation("AD user moved from '{FromOu}' to '{ToOu}', new DN: '{NewDn}'.", currentOu, targetOu, newDn);
            return Task.FromResult<AdMoveOutcome>(new AdMoveOutcome.Moved(newDn, currentOu ?? string.Empty, targetOu));
        }
        catch (DirectoryOperationException ex)
        {
            return Task.FromResult(MapDirectoryException(ex, userDistinguishedName));
        }
        catch (LdapException ex)
        {
            return Task.FromResult(MapLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdMoveOutcome>(new AdMoveOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error talking to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdMoveOutcome>(new AdMoveOutcome.TransientFailure("Network IO error: " + ex.Message, null));
        }
    }

    private bool UserExists(LdapConnection connection, string dn)
    {
        var search = new SearchRequest(dn, "(objectClass=user)", SearchScope.Base, "distinguishedName");
        try
        {
            var response = (SearchResponse)connection.SendRequest(search);
            return response.Entries.Count > 0;
        }
        catch (DirectoryOperationException ex) when (IsNoSuchObject(ex))
        {
            return false;
        }
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

    // Extrahiert den Parent-DN (alles nach dem ersten Komma).
    // "CN=John,OU=Users,DC=example,DC=local" -> "OU=Users,DC=example,DC=local"
    private static string? ExtractParentOu(string dn)
    {
        var idx = dn.IndexOf(',');
        return idx >= 0 ? dn[(idx + 1)..].Trim() : null;
    }

    // Extrahiert den RDN (alles vor dem ersten Komma).
    // "CN=John,OU=Users,DC=example,DC=local" -> "CN=John"
    private static string ExtractRdn(string dn)
    {
        var idx = dn.IndexOf(',');
        return idx >= 0 ? dn[..idx].Trim() : dn.Trim();
    }

    private static AdMoveOutcome MapDirectoryException(DirectoryOperationException ex, string dn)
    {
        var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
        if (code == NoSuchObjectCode) return new AdMoveOutcome.NotFound(dn);
        return MapResultCode(code, ex.Message);
    }

    private static AdMoveOutcome MapLdapException(LdapException ex)
        => MapResultCode(ex.ErrorCode, ex.Message);

    private static AdMoveOutcome MapResultCode(int code, string message)
    {
        return PermanentLdapCodes.Contains(code)
            ? new AdMoveOutcome.PermanentFailure(message, code >= 0 ? code : null)
            : new AdMoveOutcome.TransientFailure(message, code >= 0 ? code : null);
    }

    private static bool IsNoSuchObject(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == NoSuchObjectCode;
}
