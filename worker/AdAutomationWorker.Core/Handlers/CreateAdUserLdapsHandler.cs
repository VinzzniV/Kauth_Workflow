using System.Text.Json;
using System.Text.Json.Nodes;
using AdAutomationWorker.Core.Ad;

namespace AdAutomationWorker.Core.Handlers;

// Erster echter AD-Handler. Lebt im Core, konsumiert IAdUserWriter — damit ist die fachliche
// Logik (Payload-Validierung, Password-Generation, Outcome-Mapping) Linux-bar testbar gegen
// FakeAdUserWriter. Die LDAPS-Roundtrips passieren im Host-Adapter `LdapsAdUserWriter`.
//
// Payload-Vertrag (Pflicht):
//   samAccountName, userPrincipalName, displayName, givenName, surname, mail, targetOu
// Optional:
//   employeeNumber
//
// Initial-Status: Enabled, Random-Passwort mit Force-Change-at-Next-Logon.
//
// Etappe 9a Schritt 6 Sub-B: das generierte Initial-Passwort wird NICHT mehr plain ins
// Output-JSON geschrieben. Stattdessen liefert der Handler einen `PendingVaultWrite` mit
// und legt einen `credentialVaultId=null`-Platzhalter ins Output. Der JobStore patcht den
// Platzhalter in derselben Tx mit der erzeugten UUID. Im AlreadyExists-Pfad gibt es kein
// Initial-Passwort und damit auch keinen `PendingVaultWrite` -- der Output enthaelt dann
// `credentialVaultId=null` (Schema-stabil), und ein Welcome-Mail-Folge-Job wird zur Run-time
// einen sauberen Permanent-Failure liefern.
public sealed class CreateAdUserLdapsHandler : IWorkerHandler
{
    private readonly IAdUserWriter writer;

    public CreateAdUserLdapsHandler(IAdUserWriter writer)
    {
        this.writer = writer;
    }

    public string ActionKey => "CreateAdUserLdaps";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        // Payload-Validierung: alle Verstoesse sind klar permanent (gleicher Payload wird durch
        // Retry nicht besser). Markieren als WorkerFailureKinds.Permanent, damit die Linux-Retry-
        // Policy sofort FinalFail liefert statt MaxAttempts-Backoff.
        if (context.Payload.ValueKind != JsonValueKind.Object)
        {
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);
        }

        var samAccountName = ReadRequiredString(context.Payload, "samAccountName", out var missing1);
        var userPrincipalName = ReadRequiredString(context.Payload, "userPrincipalName", out var missing2);
        var displayName = ReadRequiredString(context.Payload, "displayName", out var missing3);
        var givenName = ReadRequiredString(context.Payload, "givenName", out var missing4);
        var surname = ReadRequiredString(context.Payload, "surname", out var missing5);
        var mail = ReadRequiredString(context.Payload, "mail", out var missing6);
        var targetOu = ReadRequiredString(context.Payload, "targetOu", out var missing7);

        var firstMissing = missing1 ?? missing2 ?? missing3 ?? missing4 ?? missing5 ?? missing6 ?? missing7;
        if (firstMissing is not null)
        {
            return WorkerHandlerResult.Failure($"Missing payload field: {firstMissing}", Array.Empty<WorkerLogEntry>(), WorkerFailureKinds.Permanent);
        }

        if (samAccountName!.Length > 20)
        {
            return WorkerHandlerResult.Failure(
                $"sAMAccountName '{samAccountName}' exceeds 20 characters (AD limit).",
                Array.Empty<WorkerLogEntry>(),
                WorkerFailureKinds.Permanent);
        }
        if (!IsSamAccountNameCharsetValid(samAccountName))
        {
            return WorkerHandlerResult.Failure(
                $"sAMAccountName '{samAccountName}' contains disallowed characters. Allowed: A-Z a-z 0-9 . - _",
                Array.Empty<WorkerLogEntry>(),
                WorkerFailureKinds.Permanent);
        }

        var employeeNumber = ReadOptionalString(context.Payload, "employeeNumber");
        var password = AdPasswordGenerator.Generate();

        var spec = new AdUserSpec
        {
            SamAccountName = samAccountName!,
            UserPrincipalName = userPrincipalName!,
            DisplayName = displayName!,
            GivenName = givenName!,
            Surname = surname!,
            Mail = mail!,
            TargetOuDn = targetOu!,
            Password = password,
            EmployeeNumber = employeeNumber,
        };

        var outcome = await writer.CreateUserAsync(spec, cancellationToken);

        return outcome switch
        {
            AdWriteOutcome.Created created => BuildCreatedResult(created, spec, context.WorkflowNodeInstanceId),
            AdWriteOutcome.AlreadyExists already => BuildAlreadyExistsResult(already, spec),
            AdWriteOutcome.PermanentFailure permanent => BuildFailureResult(permanent.LdapResultCode, permanent.Reason, WorkerFailureKinds.Permanent),
            AdWriteOutcome.TransientFailure transient => BuildFailureResult(transient.LdapResultCode, transient.Reason, WorkerFailureKinds.Transient),
            _ => WorkerHandlerResult.Failure("Unknown AdWriteOutcome variant.", Array.Empty<WorkerLogEntry>()),
        };
    }

    public async Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
    {
        if (context.Payload.ValueKind != JsonValueKind.Object)
            return WorkerPlanResult.Failure("Payload must be a JSON object.");

        var samAccountName = ReadRequiredString(context.Payload, "samAccountName", out var m1);
        var userPrincipalName = ReadRequiredString(context.Payload, "userPrincipalName", out var m2);
        var displayName = ReadRequiredString(context.Payload, "displayName", out var m3);
        var givenName = ReadRequiredString(context.Payload, "givenName", out var m4);
        var surname = ReadRequiredString(context.Payload, "surname", out var m5);
        var mail = ReadRequiredString(context.Payload, "mail", out var m6);
        var targetOu = ReadRequiredString(context.Payload, "targetOu", out var m7);

        var firstMissing = m1 ?? m2 ?? m3 ?? m4 ?? m5 ?? m6 ?? m7;
        if (firstMissing is not null)
            return WorkerPlanResult.Failure($"Missing payload field: {firstMissing}");

        var employeeNumber = ReadOptionalString(context.Payload, "employeeNumber");
        var targetDn = $"CN={displayName},{targetOu}";

        (bool exists, string? existingDn) = await writer.FindUserAsync(samAccountName!, cancellationToken);

        var plan = new
        {
            alreadyExists = exists,
            existingDn = exists ? existingDn : (string?)null,
            targetDn = exists ? (string?)null : targetDn,
            userPrincipalName,
            samAccountName,
            displayName,
            givenName,
            surname,
            mail,
            employeeNumber,
            passwordNote = exists
                ? "Bestehendes Konto — kein neues Passwort wird gesetzt."
                : "Zufälliges Initial-Passwort wird zur Ausführungszeit generiert und im Vault gespeichert.",
        };

        return WorkerPlanResult.Success(JsonSerializer.SerializeToNode(plan)!);
    }

    private static WorkerHandlerResult BuildCreatedResult(AdWriteOutcome.Created created, AdUserSpec spec, long workflowNodeInstanceId)
    {
        // credentialVaultId bleibt als null-Platzhalter im Output; der JobStore patcht ihn in
        // derselben Tx mit der UUID, die er beim Vault-Insert erzeugt. Damit ist das Schema fuer
        // Konsumenten stabil unabhaengig vom Schreibpfad.
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = created.DistinguishedName,
            samAccountName = spec.SamAccountName,
            userPrincipalName = spec.UserPrincipalName,
            credentialVaultId = (string?)null,
            mustChangePasswordAtNextLogon = true,
            forceChangeReason = "initial",
            employeeNumber = spec.EmployeeNumber,
            alreadyExisted = false,
        });

        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = "User created (random password set, force-change at next logon; password stored in vault).",
            Details = JsonSerializer.SerializeToElement(new
            {
                samAccountName = spec.SamAccountName,
                distinguishedName = created.DistinguishedName,
            }),
        };

        var vaultWrite = new PendingVaultWrite
        {
            PlainSecret = spec.Password,
            WorkflowNodeInstanceId = workflowNodeInstanceId,
            CredentialType = "ad_initial_password",
        };

        return WorkerHandlerResult.Success(output, new[] { log }) with { VaultWrite = vaultWrite };
    }

    private static WorkerHandlerResult BuildAlreadyExistsResult(AdWriteOutcome.AlreadyExists already, AdUserSpec spec)
    {
        // Kein Vault-Schreib bei AlreadyExists -- das Konto existierte bereits, das Initial-Passwort
        // ist hier nicht relevant. credentialVaultId bleibt null und kommt so ins Output. Ein
        // Welcome-Mail-Folge-Job mit Vault-Verkettung wird daran sauber Permanent-fail.
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = already.DistinguishedName,
            samAccountName = spec.SamAccountName,
            userPrincipalName = spec.UserPrincipalName,
            credentialVaultId = (string?)null,
            alreadyExisted = true,
            employeeNumber = spec.EmployeeNumber,
        });

        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = "User already existed; treating as success (idempotent).",
            Details = JsonSerializer.SerializeToElement(new
            {
                samAccountName = spec.SamAccountName,
                distinguishedName = already.DistinguishedName,
            }),
        };

        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildFailureResult(int? ldapResultCode, string reason, string failureKind)
    {
        var prefix = ldapResultCode.HasValue ? $"LDAP {ldapResultCode.Value}: " : string.Empty;
        var errorMessage = $"{prefix}{reason}";
        var log = new WorkerLogEntry
        {
            Level = "error",
            Message = errorMessage,
            Details = JsonSerializer.SerializeToElement(new
            {
                ldapResultCode,
                failureKind,
            }),
        };
        return WorkerHandlerResult.Failure(errorMessage, new[] { log }, failureKind);
    }

    private static string? ReadRequiredString(JsonElement payload, string property, out string? missingName)
    {
        if (!payload.TryGetProperty(property, out var value))
        {
            missingName = property;
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            missingName = property;
            return null;
        }
        var raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            missingName = property;
            return null;
        }
        missingName = null;
        return raw.Trim();
    }

    private static string? ReadOptionalString(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        var raw = value.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static bool IsSamAccountNameCharsetValid(string s)
    {
        foreach (var c in s)
        {
            var allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
            if (!allowed) return false;
        }
        return true;
    }
}
