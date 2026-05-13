using System.DirectoryServices.Protocols;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// Zweiter LDAPS-Schreiber im Worker (Etappe 9a Schritt 5 Sub-B). Fuegt einen User in N Gruppen
// ein via ModifyRequest auf das Group-DN mit Operation `Add` auf das `member`-Attribut.
//
// Idempotenz: Code 20 (AttributeOrValueAlreadyExists) wird als AlreadyMember behandelt — eine
// erneute Ausfuehrung mit denselben Groups landet sauber als AllAdded mit allen Groups im
// AlreadyMember-Bucket.
//
// Error-Mapping identisch zu LdapsAdUserWriter (Whitelist 49/50/32/21/19 = permanent). Bei
// Connection-Level-Fehlern (alle Groups schlagen mit demselben transient-Error fehl) wird ein
// Top-Level TransientFailure geliefert statt PartiallyAdded.
internal sealed class LdapsAdGroupMembershipWriter : IAdGroupMembershipWriter
{
    private const int LdapPort = 636;
    private const int AlreadyMemberCode = 20;

    // Gleiche Whitelist wie LdapsAdUserWriter (Etappe 9a Schritt 3 + 4): klare Konfigurations-/
    // Berechtigungs-Fehler, die durch Retry nicht besser werden.
    private static readonly HashSet<int> PermanentLdapCodes = new()
    {
        49, // InvalidCredentials
        50, // InsufficientAccessRights
        32, // NoSuchObject
        21, // InvalidAttributeSyntax
        19, // ConstraintViolation
    };

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdGroupMembershipWriter> logger;

    public LdapsAdGroupMembershipWriter(IOptions<WorkerSettings> options, ILogger<LdapsAdGroupMembershipWriter> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<AdGroupMembershipOutcome> AddMembershipsAsync(AdGroupMembershipSpec spec, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
        {
            return Task.FromResult<AdGroupMembershipOutcome>(
                new AdGroupMembershipOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));
        }

        LdapConnection connection;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection = OpenConnection();
        }
        catch (LdapException ex)
        {
            return Task.FromResult<AdGroupMembershipOutcome>(MapTopLevelLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdGroupMembershipOutcome>(new AdGroupMembershipOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error opening LDAP connection to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdGroupMembershipOutcome>(
                new AdGroupMembershipOutcome.TransientFailure("Network IO error: " + ex.Message, null));
        }

        using (connection)
        {
            var newlyAdded = new List<string>();
            var alreadyMember = new List<string>();
            var failures = new List<GroupFailure>();

            foreach (var groupDn in spec.GroupDistinguishedNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    AddUserToGroup(connection, groupDn, spec.UserDistinguishedName);
                    newlyAdded.Add(groupDn);
                }
                catch (DirectoryOperationException ex) when (IsAlreadyMember(ex))
                {
                    alreadyMember.Add(groupDn);
                }
                catch (DirectoryOperationException ex)
                {
                    var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
                    failures.Add(new GroupFailure(groupDn, code, ex.Message, PermanentLdapCodes.Contains(code)));
                }
                catch (LdapException ex)
                {
                    failures.Add(new GroupFailure(groupDn, ex.ErrorCode, ex.Message, PermanentLdapCodes.Contains(ex.ErrorCode)));
                }
            }

            if (failures.Count > 0 && newlyAdded.Count == 0 && alreadyMember.Count == 0)
            {
                // Alle Groups gleich gescheitert — typischerweise Connection-Level. Aggregiere zu
                // einem Top-Level-Failure, damit der Worker-Handler kein leeres PartiallyAdded
                // ausliefert.
                var allPermanent = failures.All(f => f.IsPermanent);
                var firstReason = failures[0].Reason;
                var firstCode = failures[0].LdapResultCode;
                if (allPermanent)
                {
                    return Task.FromResult<AdGroupMembershipOutcome>(new AdGroupMembershipOutcome.PermanentFailure(firstReason, firstCode));
                }
                return Task.FromResult<AdGroupMembershipOutcome>(new AdGroupMembershipOutcome.TransientFailure(firstReason, firstCode));
            }

            if (failures.Count == 0)
            {
                return Task.FromResult<AdGroupMembershipOutcome>(new AdGroupMembershipOutcome.AllAdded(newlyAdded, alreadyMember));
            }

            return Task.FromResult<AdGroupMembershipOutcome>(
                new AdGroupMembershipOutcome.PartiallyAdded(newlyAdded, alreadyMember, failures));
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

    private static void AddUserToGroup(LdapConnection connection, string groupDn, string userDn)
    {
        var modify = new ModifyRequest(
            groupDn,
            new DirectoryAttributeModification
            {
                Name = "member",
                Operation = DirectoryAttributeOperation.Add,
            });
        modify.Modifications[0].Add(userDn);
        connection.SendRequest(modify);
    }

    private static bool IsAlreadyMember(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == AlreadyMemberCode;

    private static AdGroupMembershipOutcome MapTopLevelLdapException(LdapException ex)
    {
        var code = ex.ErrorCode;
        return PermanentLdapCodes.Contains(code)
            ? new AdGroupMembershipOutcome.PermanentFailure(ex.Message, code)
            : new AdGroupMembershipOutcome.TransientFailure(ex.Message, code);
    }
}
