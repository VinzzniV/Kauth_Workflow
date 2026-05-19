using System.DirectoryServices.Protocols;
using System.Text;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// LDAPS-Implementierung fuer IAdGroupMembershipRemover. Entfernt einen AD-User aus allen
// Gruppen, in denen er Mitglied ist.
//
// Ablauf:
//   1. SearchRequest im BaseDn-Subtree: `(&(objectClass=group)(member=<escaped_dn>))`.
//      Liefert alle Gruppen-DNs.
//   2. Pro Gruppe: ModifyRequest mit Operation=Delete auf das `member`-Attribut.
//   3. Code 16 (NoSuchAttribute = User war kein Mitglied) -> AlreadyRemoved (idempotent).
//   4. Whitelist 49/50/32/21/19 -> PermanentFailure pro Gruppe. Alles andere -> Transient.
//   5. Connection-Fehler vor der Suche -> Top-Level TransientFailure.
internal sealed class LdapsAdGroupMembershipRemover : IAdGroupMembershipRemover
{
    private const int LdapPort = 636;
    private const int AlreadyRemovedCode = 16; // NoSuchAttribute

    private static readonly HashSet<int> PermanentLdapCodes = new()
    {
        49, // InvalidCredentials
        50, // InsufficientAccessRights
        32, // NoSuchObject
        21, // InvalidAttributeSyntax
        19, // ConstraintViolation
    };

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdGroupMembershipRemover> logger;

    public LdapsAdGroupMembershipRemover(IOptions<WorkerSettings> options, ILogger<LdapsAdGroupMembershipRemover> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<IReadOnlyList<string>> FindMemberOfGroupsAsync(
        string userDistinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            throw new InvalidOperationException("Worker AdSettings.DcHost is not configured.");
        if (string.IsNullOrWhiteSpace(settings.BaseDn))
            throw new InvalidOperationException("Worker AdSettings.BaseDn is not configured.");

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        var groups = FindMemberOfGroups(connection, userDistinguishedName);
        return Task.FromResult<IReadOnlyList<string>>(groups);
    }

    public Task<AdRemoveMembershipsOutcome> RemoveAllMembershipsAsync(
        string userDistinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            return Task.FromResult<AdRemoveMembershipsOutcome>(
                new AdRemoveMembershipsOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));
        if (string.IsNullOrWhiteSpace(settings.BaseDn))
            return Task.FromResult<AdRemoveMembershipsOutcome>(
                new AdRemoveMembershipsOutcome.PermanentFailure("Worker AdSettings.BaseDn is not configured.", null));

        LdapConnection connection;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection = OpenConnection();
        }
        catch (LdapException ex)
        {
            return Task.FromResult(MapTopLevelLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdRemoveMembershipsOutcome>(new AdRemoveMembershipsOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error opening LDAP connection to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdRemoveMembershipsOutcome>(
                new AdRemoveMembershipsOutcome.TransientFailure("Network IO error: " + ex.Message, null));
        }

        using (connection)
        {
            List<string> groupDns;
            try
            {
                groupDns = FindMemberOfGroups(connection, userDistinguishedName);
            }
            catch (LdapException ex)
            {
                return Task.FromResult(MapTopLevelLdapException(ex));
            }
            catch (DirectoryOperationException ex)
            {
                var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
                var isPerm = PermanentLdapCodes.Contains(code);
                return Task.FromResult<AdRemoveMembershipsOutcome>(
                    isPerm
                        ? new AdRemoveMembershipsOutcome.PermanentFailure(ex.Message, code >= 0 ? code : null)
                        : new AdRemoveMembershipsOutcome.TransientFailure(ex.Message, code >= 0 ? code : null));
            }

            if (groupDns.Count == 0)
            {
                logger.LogInformation("User '{Dn}' has no group memberships to remove (idempotent).", userDistinguishedName);
                return Task.FromResult<AdRemoveMembershipsOutcome>(
                    new AdRemoveMembershipsOutcome.AllRemoved(Array.Empty<string>(), Array.Empty<string>()));
            }

            var removed = new List<string>();
            var alreadyRemoved = new List<string>();
            var failures = new List<GroupRemoveFailure>();

            foreach (var groupDn in groupDns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    RemoveUserFromGroup(connection, groupDn, userDistinguishedName);
                    removed.Add(groupDn);
                }
                catch (DirectoryOperationException ex) when (IsAlreadyRemoved(ex))
                {
                    alreadyRemoved.Add(groupDn);
                }
                catch (DirectoryOperationException ex)
                {
                    var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
                    failures.Add(new GroupRemoveFailure(groupDn, code >= 0 ? code : null, ex.Message, PermanentLdapCodes.Contains(code)));
                }
                catch (LdapException ex)
                {
                    failures.Add(new GroupRemoveFailure(groupDn, ex.ErrorCode, ex.Message, PermanentLdapCodes.Contains(ex.ErrorCode)));
                }
            }

            if (failures.Count > 0 && removed.Count == 0 && alreadyRemoved.Count == 0)
            {
                var allPermanent = failures.All(f => f.IsPermanent);
                var firstReason = failures[0].Reason;
                var firstCode = failures[0].LdapResultCode;
                return allPermanent
                    ? Task.FromResult<AdRemoveMembershipsOutcome>(new AdRemoveMembershipsOutcome.PermanentFailure(firstReason, firstCode))
                    : Task.FromResult<AdRemoveMembershipsOutcome>(new AdRemoveMembershipsOutcome.TransientFailure(firstReason, firstCode));
            }

            if (failures.Count == 0)
            {
                logger.LogInformation("User '{Dn}' removed from {Count} group(s).", userDistinguishedName, removed.Count);
                return Task.FromResult<AdRemoveMembershipsOutcome>(new AdRemoveMembershipsOutcome.AllRemoved(removed, alreadyRemoved));
            }

            return Task.FromResult<AdRemoveMembershipsOutcome>(
                new AdRemoveMembershipsOutcome.PartiallyRemoved(removed, alreadyRemoved, failures));
        }
    }

    private List<string> FindMemberOfGroups(LdapConnection connection, string userDn)
    {
        var escaped = EscapeLdapDn(userDn);
        var search = new SearchRequest(
            settings.BaseDn,
            $"(&(objectClass=group)(member={escaped}))",
            SearchScope.Subtree,
            "distinguishedName");

        var response = (SearchResponse)connection.SendRequest(search);
        var result = new List<string>(response.Entries.Count);
        foreach (SearchResultEntry entry in response.Entries)
        {
            result.Add(entry.DistinguishedName);
        }
        return result;
    }

    private static void RemoveUserFromGroup(LdapConnection connection, string groupDn, string userDn)
    {
        var modify = new ModifyRequest(
            groupDn,
            new DirectoryAttributeModification
            {
                Name = "member",
                Operation = DirectoryAttributeOperation.Delete,
            });
        modify.Modifications[0].Add(userDn);
        connection.SendRequest(modify);
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

    private static bool IsAlreadyRemoved(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == AlreadyRemovedCode;

    private static AdRemoveMembershipsOutcome MapTopLevelLdapException(LdapException ex)
    {
        return PermanentLdapCodes.Contains(ex.ErrorCode)
            ? new AdRemoveMembershipsOutcome.PermanentFailure(ex.Message, ex.ErrorCode)
            : new AdRemoveMembershipsOutcome.TransientFailure(ex.Message, ex.ErrorCode);
    }

    private static string EscapeLdapDn(string value)
    {
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
