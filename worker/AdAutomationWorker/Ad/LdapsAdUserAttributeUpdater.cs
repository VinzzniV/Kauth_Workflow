using System.DirectoryServices.Protocols;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker.Ad;

// LDAPS-Implementierung für IAdUserAttributeUpdater. Aktualisiert eine feste Whitelist von
// AD-User-Attributen via ModifyRequest (Replace).
//
// Whitelist (LDAP): manager, department, title, description.
// null = Attribut löschen (Replace auf leere Liste -> AD entfernt das Attribut).
//
// Ablauf:
//   1. SearchRequest (Scope=Base) auf DN, liest aktuelle Whitelist-Werte.
//      Kein Eintrag -> NotFound.
//   2. Vergleich: nur tatsächlich geänderte Attribute in den ModifyRequest aufnehmen.
//      Keine Änderungen -> NoChangesNeeded.
//   3. ModifyRequest mit Replace-Operationen für geänderte Attribute.
//   4. Error-Mapping: Whitelist 49/50/32/21/19 -> Permanent. Alles andere -> Transient.
internal sealed class LdapsAdUserAttributeUpdater : IAdUserAttributeUpdater
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

    // LDAP-Attribute die aus der DB gelesen werden (für den Snapshot).
    private static readonly string[] WhitelistLdapAttrs = ["manager", "department", "title", "description"];

    private readonly AdSettings settings;
    private readonly ILogger<LdapsAdUserAttributeUpdater> logger;

    public LdapsAdUserAttributeUpdater(IOptions<WorkerSettings> options, ILogger<LdapsAdUserAttributeUpdater> logger)
    {
        this.settings = options.Value.Ad;
        this.logger = logger;
    }

    public Task<(bool Exists, AdUserAttributeSnapshot? Snapshot)> GetUserAttributesAsync(
        string distinguishedName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            throw new InvalidOperationException("Worker AdSettings.DcHost is not configured.");

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        var (exists, snapshot) = ReadAttributes(connection, distinguishedName);
        return Task.FromResult((exists, exists ? snapshot : (AdUserAttributeSnapshot?)null));
    }

    public Task<AdUpdateAttributesOutcome> UpdateAttributesAsync(
        AdUserAttributeUpdateSpec spec, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DcHost))
            return Task.FromResult<AdUpdateAttributesOutcome>(
                new AdUpdateAttributesOutcome.PermanentFailure("Worker AdSettings.DcHost is not configured.", null));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();

            var (exists, current) = ReadAttributes(connection, spec.DistinguishedName);
            if (!exists)
            {
                logger.LogWarning("AD user not found at DN '{Dn}' — cannot update attributes.", spec.DistinguishedName);
                return Task.FromResult<AdUpdateAttributesOutcome>(new AdUpdateAttributesOutcome.NotFound(spec.DistinguishedName));
            }

            // Bestimme welche Attribute sich tatsächlich ändern.
            var changes = BuildChanges(spec.Attributes, current!);
            if (changes.Count == 0)
            {
                logger.LogInformation("No attribute changes needed for '{Dn}' (all values already set).", spec.DistinguishedName);
                return Task.FromResult<AdUpdateAttributesOutcome>(new AdUpdateAttributesOutcome.NoChangesNeeded(spec.DistinguishedName));
            }

            var modifyRequest = new ModifyRequest(spec.DistinguishedName);
            foreach (var (ldapAttr, newValue) in changes)
            {
                var mod = new DirectoryAttributeModification
                {
                    Name = ldapAttr,
                    Operation = DirectoryAttributeOperation.Replace,
                };
                if (newValue is not null)
                {
                    mod.Add(newValue);
                }
                // newValue == null -> leere Replace-Operation -> AD löscht das Attribut
                modifyRequest.Modifications.Add(mod);
            }

            connection.SendRequest(modifyRequest);

            var changedAttrNames = changes.Keys.ToList();
            logger.LogInformation("AD user attributes updated for '{Dn}': {Attrs}.", spec.DistinguishedName, string.Join(", ", changedAttrNames));
            return Task.FromResult<AdUpdateAttributesOutcome>(
                new AdUpdateAttributesOutcome.Updated(spec.DistinguishedName, changedAttrNames));
        }
        catch (DirectoryOperationException ex)
        {
            return Task.FromResult(MapDirectoryException(ex, spec.DistinguishedName));
        }
        catch (LdapException ex)
        {
            return Task.FromResult(MapLdapException(ex));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<AdUpdateAttributesOutcome>(new AdUpdateAttributesOutcome.TransientFailure("Operation cancelled.", null));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "IO error talking to {DcHost}.", settings.DcHost);
            return Task.FromResult<AdUpdateAttributesOutcome>(new AdUpdateAttributesOutcome.TransientFailure("Network IO error: " + ex.Message, null));
        }
    }

    // Liest aktuelle Whitelist-Werte. Gibt (false, null) wenn User nicht gefunden.
    private (bool Exists, AdUserAttributeSnapshot? Snapshot) ReadAttributes(LdapConnection connection, string dn)
    {
        var search = new SearchRequest(dn, "(objectClass=user)", SearchScope.Base, WhitelistLdapAttrs);
        SearchResponse response;
        try
        {
            response = (SearchResponse)connection.SendRequest(search);
        }
        catch (DirectoryOperationException ex) when (IsNoSuchObject(ex))
        {
            return (false, null);
        }

        if (response.Entries.Count == 0) return (false, null);

        var entry = response.Entries[0];
        var snapshot = new AdUserAttributeSnapshot
        {
            Manager = ReadAttr(entry, "manager"),
            Department = ReadAttr(entry, "department"),
            Title = ReadAttr(entry, "title"),
            Description = ReadAttr(entry, "description"),
        };
        return (true, snapshot);
    }

    // Vergleicht gewünschte Werte mit aktuellen; gibt nur tatsächlich geänderte zurück.
    private static Dictionary<string, string?> BuildChanges(
        IReadOnlyDictionary<string, string?> requested,
        AdUserAttributeSnapshot current)
    {
        var currentMap = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["manager"] = current.Manager,
            ["department"] = current.Department,
            ["title"] = current.Title,
            ["description"] = current.Description,
        };

        var changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (ldapAttr, newValue) in requested)
        {
            currentMap.TryGetValue(ldapAttr, out var currentValue);
            // Normalisiere leeren String zu null für den Vergleich.
            var normalizedNew = string.IsNullOrEmpty(newValue) ? null : newValue;
            var normalizedCurrent = string.IsNullOrEmpty(currentValue) ? null : currentValue;
            if (!string.Equals(normalizedNew, normalizedCurrent, StringComparison.Ordinal))
            {
                changes[ldapAttr] = normalizedNew;
            }
        }
        return changes;
    }

    private static string? ReadAttr(SearchResultEntry entry, string attrName)
    {
        if (!entry.Attributes.Contains(attrName)) return null;
        var attr = entry.Attributes[attrName];
        if (attr.Count == 0) return null;
        return attr[0]?.ToString();
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

    private static AdUpdateAttributesOutcome MapDirectoryException(DirectoryOperationException ex, string dn)
    {
        var code = ex.Response?.ResultCode is { } rc ? (int)rc : -1;
        if (code == NoSuchObjectCode) return new AdUpdateAttributesOutcome.NotFound(dn);
        return MapResultCode(code, ex.Message);
    }

    private static AdUpdateAttributesOutcome MapLdapException(LdapException ex)
        => MapResultCode(ex.ErrorCode, ex.Message);

    private static AdUpdateAttributesOutcome MapResultCode(int code, string message)
    {
        return PermanentLdapCodes.Contains(code)
            ? new AdUpdateAttributesOutcome.PermanentFailure(message, code >= 0 ? code : null)
            : new AdUpdateAttributesOutcome.TransientFailure(message, code >= 0 ? code : null);
    }

    private static bool IsNoSuchObject(DirectoryOperationException ex)
        => ex.Response?.ResultCode is { } rc && (int)rc == NoSuchObjectCode;
}
