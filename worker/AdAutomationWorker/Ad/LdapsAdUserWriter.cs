using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// Windows-only LDAPS-Schreiber. Lebt im net8.0-windows-Host, weil
// System.DirectoryServices.Protocols Windows-only ist.
//
// Authentisierung: AuthType.Negotiate. Service laeuft unter dem gMSA-Kontext; SSPI/Kerberos
// laeuft transparent — keine expliziten Credentials hier.
//
// Flow:
//   1. Pre-Search auf (&(objectClass=user)(sAMAccountName=<escaped>)) im BaseDn-Subtree.
//      Treffer -> AlreadyExists mit DN aus dem Treffer.
//   2. AddRequest legt den User initial disabled an (userAccountControl=0x202).
//   3. ModifyRequest setzt unicodePwd (UTF-16-LE in quotes), aktiviert (uac=0x200) und setzt
//      pwdLastSet=0 (Force-Change). Eine eigene Operation, weil AD bei Password-Set ueber
//      LDAPS keine Mixed-Modify-Calls akzeptiert.
//   4. Error-Mapping: Code 68 -> AlreadyExists. Permanent-Whitelist (49/50/32/21/19) ->
//      PermanentFailure. Alles andere (51/52/81/IO/Cancel) -> TransientFailure.
internal sealed class LdapsAdUserWriter : IAdUserWriter
{
    private const int LdapPort = 636;

    // Permanent-Whitelist (User-Entscheidung 2026-05-12): klare Konfigurations-/Berechtigungs-
    // fehler, die durch Retry nicht besser werden. Alles andere bleibt transient.
    private static readonly HashSet<int> PermanentLdapCodes = new()
    {
        49, // InvalidCredentials
        50, // InsufficientAccessRights
        32, // NoSuchObject (z. B. invalides Target-OU)
        21, // InvalidAttributeSyntax
        19, // ConstraintViolation
    };

    private const int AlreadyExistsCode = 68;

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdUserWriter> logger;

    public LdapsAdUserWriter(IOptions<WorkerSettings> options, ILogger<LdapsAdUserWriter> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<(bool Exists, string? DistinguishedName)> FindUserAsync(
        string samAccountName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            throw new InvalidOperationException("Worker AdSettings.DcHost is not configured.");
        if (string.IsNullOrWhiteSpace(settings.BaseDn))
            throw new InvalidOperationException("Worker AdSettings.BaseDn is not configured.");

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        var dn = FindExistingUserDn(connection, samAccountName);
        return Task.FromResult(dn is not null ? (true, dn) : (false, (string?)null));
    }

    public Task<AdWriteOutcome> CreateUserAsync(AdUserSpec spec, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
        {
            return Task.FromResult<AdWriteOutcome>(
                new AdWriteOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));
        }
        if (string.IsNullOrWhiteSpace(settings.BaseDn))
        {
            return Task.FromResult<AdWriteOutcome>(
                new AdWriteOutcome.PermanentFailure("Worker AdSettings.BaseDn is not configured.", null));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = OpenConnection();

            var existingDn = FindExistingUserDn(connection, spec.SamAccountName);
            if (existingDn is not null)
            {
                logger.LogInformation("AD user '{Sam}' already exists at {Dn}.", spec.SamAccountName, existingDn);
                return Task.FromResult<AdWriteOutcome>(new AdWriteOutcome.AlreadyExists(existingDn));
            }

            var dn = spec.BuildDistinguishedName();
            try
            {
                AddDisabledUser(connection, dn, spec);
            }
            catch (DirectoryOperationException raceEx) when (IsAlreadyExists(raceEx))
            {
                // Race: Pre-Search hat den User nicht gefunden, der ADD trifft auf einen parallel
                // angelegten Eintrag. Nochmal suchen, um den tatsaechlichen DN zu liefern.
                var raceDn = FindExistingUserDn(connection, spec.SamAccountName) ?? dn;
                logger.LogInformation("AD user '{Sam}' race-existed at {Dn}; treating as success.", spec.SamAccountName, raceDn);
                return Task.FromResult<AdWriteOutcome>(new AdWriteOutcome.AlreadyExists(raceDn));
            }

            try
            {
                EnableAndSetPassword(connection, dn, spec.Password);
            }
            catch
            {
                // Rollback: User wurde angelegt aber Password-Set ist gescheitert -> User ist
                // disabled und ohne Passwort. AD wuerde so einen Eintrag liegen lassen; wir
                // versuchen ihn zu loeschen, damit der naechste Retry einen sauberen Start hat.
                TryDeleteEntry(connection, dn);
                throw;
            }

            logger.LogInformation("AD user '{Sam}' created at {Dn}.", spec.SamAccountName, dn);
            return Task.FromResult<AdWriteOutcome>(new AdWriteOutcome.Created(dn));
        }
        catch (DirectoryOperationException ex)
        {
            return Task.FromResult(MapDirectoryException(ex));
        }
        catch (LdapException ex)
        {
            return Task.FromResult(MapLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdWriteOutcome>(new AdWriteOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error talking to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdWriteOutcome>(new AdWriteOutcome.TransientFailure("Network IO error: " + ex.Message, null));
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
        connection.Bind(); // gMSA-Kontext liefert die Credentials transparent.
        return connection;
    }

    private string? FindExistingUserDn(LdapConnection connection, string samAccountName)
    {
        var escaped = EscapeLdapFilter(samAccountName);
        var search = new SearchRequest(
            settings.BaseDn,
            $"(&(objectClass=user)(sAMAccountName={escaped}))",
            SearchScope.Subtree,
            "distinguishedName");

        var response = (SearchResponse)connection.SendRequest(search);
        return response.Entries.Count == 0 ? null : response.Entries[0].DistinguishedName;
    }

    private static void AddDisabledUser(LdapConnection connection, string dn, AdUserSpec spec)
    {
        var add = new AddRequest(dn);
        add.Attributes.Add(new DirectoryAttribute("objectClass", "user"));
        add.Attributes.Add(new DirectoryAttribute("sAMAccountName", spec.SamAccountName));
        add.Attributes.Add(new DirectoryAttribute("userPrincipalName", spec.UserPrincipalName));
        add.Attributes.Add(new DirectoryAttribute("cn", spec.DisplayName));
        add.Attributes.Add(new DirectoryAttribute("displayName", spec.DisplayName));
        add.Attributes.Add(new DirectoryAttribute("givenName", spec.GivenName));
        add.Attributes.Add(new DirectoryAttribute("sn", spec.Surname));
        add.Attributes.Add(new DirectoryAttribute("mail", spec.Mail));
        if (!string.IsNullOrWhiteSpace(spec.EmployeeNumber))
        {
            add.Attributes.Add(new DirectoryAttribute("employeeID", spec.EmployeeNumber));
        }
        // 0x202 = NORMAL_ACCOUNT (0x200) | ACCOUNTDISABLE (0x2). Initial disabled — wird im
        // Modify-Schritt zusammen mit dem Passwort enabled.
        add.Attributes.Add(new DirectoryAttribute("userAccountControl", "514"));

        connection.SendRequest(add);
    }

    private static void EnableAndSetPassword(LdapConnection connection, string dn, string password)
    {
        // Pflicht: UTF-16-LE Bytes von '"' + password + '"'. AD verlangt die Quotes als Teil
        // der Bytes; ohne die Quotes wirft AD ConstraintViolation.
        var quoted = "\"" + password + "\"";
        var pwdBytes = Encoding.Unicode.GetBytes(quoted);

        var modify = new ModifyRequest(
            dn,
            new DirectoryAttributeModification
            {
                Name = "unicodePwd",
                Operation = DirectoryAttributeOperation.Replace,
            },
            new DirectoryAttributeModification
            {
                Name = "userAccountControl",
                Operation = DirectoryAttributeOperation.Replace,
            },
            new DirectoryAttributeModification
            {
                Name = "pwdLastSet",
                Operation = DirectoryAttributeOperation.Replace,
            });

        modify.Modifications[0].Add(pwdBytes);
        modify.Modifications[1].Add("512"); // 0x200 = NORMAL_ACCOUNT, ACCOUNTDISABLE-Bit entfernt.
        modify.Modifications[2].Add("0");   // pwdLastSet=0 => Force-Change-at-Next-Logon.

        connection.SendRequest(modify);
    }

    private void TryDeleteEntry(LdapConnection connection, string dn)
    {
        try
        {
            connection.SendRequest(new DeleteRequest(dn));
            logger.LogWarning("Rolled back partially created AD user at {Dn}.", dn);
        }
        catch (Exception ex)
        {
            // Wenn Rollback scheitert: stehen lassen + loggen. Naechster Retry findet via Pre-Search
            // den disabled User und liefert AlreadyExists; manuelle Bereinigung ist nicht
            // ausgeschlossen, aber selten.
            logger.LogError(ex, "Failed to rollback partial AD user at {Dn}; manual cleanup may be required.", dn);
        }
    }

    private static AdWriteOutcome MapDirectoryException(DirectoryOperationException ex)
    {
        var code = ex.Response?.ResultCode is { } resultCode ? (int)resultCode : -1;
        return MapResultCode(code, ex.Message);
    }

    private static AdWriteOutcome MapLdapException(LdapException ex)
        => MapResultCode(ex.ErrorCode, ex.Message);

    private static AdWriteOutcome MapResultCode(int code, string message)
    {
        if (PermanentLdapCodes.Contains(code))
        {
            return new AdWriteOutcome.PermanentFailure(message, code >= 0 ? code : null);
        }
        return new AdWriteOutcome.TransientFailure(message, code >= 0 ? code : null);
    }

    private static bool IsAlreadyExists(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == AlreadyExistsCode;

    private static string EscapeLdapFilter(string value)
    {
        // RFC 4515 — Sonderzeichen in LDAP-Filtern.
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
