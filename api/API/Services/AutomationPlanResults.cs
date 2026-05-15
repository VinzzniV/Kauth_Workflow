using System.Text.Json.Nodes;

namespace API;

internal record AutomationPlanContext(
    string WorkflowInstanceUid,
    string ActionKey,
    System.Text.Json.JsonDocument Payload);

internal record AutomationLinuxPlanResult(bool IsSuccess, JsonNode? Plan, string? ErrorMessage)
{
    public static AutomationLinuxPlanResult NotSupported(string actionKey) =>
        new(false, null, $"PlanAsync not supported for action '{actionKey}'");

    public static AutomationLinuxPlanResult Success(JsonNode plan) =>
        new(true, plan, null);

    public static AutomationLinuxPlanResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}

// Plan-Ergebnis für CreateAdUserLdaps
internal record AdUserPlan(
    bool AlreadyExists,
    string? ExistingDn,
    string? TargetDn,
    string? UserPrincipalName,
    string? SamAccountName,
    string? DisplayName,
    string? GivenName,
    string? Surname,
    string? Mail,
    string? EmployeeNumber,
    string PasswordNote);

// Plan-Ergebnis für AssignGroupsLdaps
internal record GroupAssignmentPlan(
    string? UserDistinguishedName,
    IReadOnlyList<PlannedGroup> Groups,
    string? Note);

internal record PlannedGroup(
    string GroupDn,
    bool? AlreadyMember,
    string? Source);

// Plan-Ergebnis für CreateMailboxGraph
internal record MailboxPlan(
    bool UpnKnown,
    string? UserPrincipalName,
    string SkuId,
    string? SkuDisplayName,
    bool SmtpAddressKnown,
    string SmtpNote,
    string? UpnNote);

// Plan-Ergebnis für SendWelcomeMailGraph
internal record WelcomeMailPlan(
    bool RecipientKnown,
    string? Recipient,
    string? Subject,
    string? BodyPreview,
    PasswordAvailability PasswordAvailability,
    string Note);

internal enum PasswordAvailability
{
    PresentInVault,     // echte UUID — Vault-Eintrag bereits vorhanden (Vorgänger succeeded)
    WillBeGenerated,    // "plan:will-be-generated" — AD-Plan bestätigt AlreadyExists=false
    Unknown,            // "plan:credential-status-unknown" — AD-Vorgänger noch nicht geplant
    NotAvailable        // null — AlreadyExists-Pfad, kein Vault-Eintrag
}

// Aggregiertes Plan-Ergebnis für einen Workflow-Node (enthält alle Action-Schritte)
internal record NodePlanResult(
    string NodeKey,
    IReadOnlyList<ActionPlanStep> Steps);

internal record ActionPlanStep(
    string ActionKey,
    bool IsSuccess,
    JsonNode? Plan,
    string? ErrorMessage);
