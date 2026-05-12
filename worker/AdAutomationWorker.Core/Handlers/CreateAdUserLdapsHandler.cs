using System.Text.Json;
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
// Initial-Status: Enabled, Random-Passwort mit Force-Change-at-Next-Logon. temporaryPassword
// landet im Output (automation_job_attempts.output_json) — bewusst akzeptiertes Audit-Risiko,
// Vault-Pointer kommt im Folge-Slice.
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
        if (context.Payload.ValueKind != JsonValueKind.Object)
        {
            return WorkerHandlerResult.Failure("Payload must be a JSON object.", Array.Empty<WorkerLogEntry>());
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
            return WorkerHandlerResult.Failure($"Missing payload field: {firstMissing}", Array.Empty<WorkerLogEntry>());
        }

        if (samAccountName!.Length > 20)
        {
            return WorkerHandlerResult.Failure(
                $"sAMAccountName '{samAccountName}' exceeds 20 characters (AD limit).",
                Array.Empty<WorkerLogEntry>());
        }
        if (!IsSamAccountNameCharsetValid(samAccountName))
        {
            return WorkerHandlerResult.Failure(
                $"sAMAccountName '{samAccountName}' contains disallowed characters. Allowed: A-Z a-z 0-9 . - _",
                Array.Empty<WorkerLogEntry>());
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
            AdWriteOutcome.Created created => BuildCreatedResult(created, spec),
            AdWriteOutcome.AlreadyExists already => BuildAlreadyExistsResult(already, spec),
            AdWriteOutcome.PermanentFailure permanent => BuildFailureResult(permanent.LdapResultCode, permanent.Reason, "permanent"),
            AdWriteOutcome.TransientFailure transient => BuildFailureResult(transient.LdapResultCode, transient.Reason, "transient"),
            _ => WorkerHandlerResult.Failure("Unknown AdWriteOutcome variant.", Array.Empty<WorkerLogEntry>()),
        };
    }

    private static WorkerHandlerResult BuildCreatedResult(AdWriteOutcome.Created created, AdUserSpec spec)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = created.DistinguishedName,
            samAccountName = spec.SamAccountName,
            userPrincipalName = spec.UserPrincipalName,
            temporaryPassword = spec.Password,
            mustChangePasswordAtNextLogon = true,
            forceChangeReason = "initial",
            employeeNumber = spec.EmployeeNumber,
            alreadyExisted = false,
        });

        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = "User created (random password set, force-change at next logon).",
            Details = JsonSerializer.SerializeToElement(new
            {
                samAccountName = spec.SamAccountName,
                distinguishedName = created.DistinguishedName,
            }),
        };

        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static WorkerHandlerResult BuildAlreadyExistsResult(AdWriteOutcome.AlreadyExists already, AdUserSpec spec)
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            distinguishedName = already.DistinguishedName,
            samAccountName = spec.SamAccountName,
            userPrincipalName = spec.UserPrincipalName,
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
        return WorkerHandlerResult.Failure(errorMessage, new[] { log });
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
