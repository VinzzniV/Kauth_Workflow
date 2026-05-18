namespace API;

// Slice 5 (Admin-Gated-Automation, Referenzuser-Mapping): Read-only-Vertrag fuer
// den Plan-Service-Pfad. Liefert Cloud-Gruppen eines via person_lookup-Antwort
// gewaehlten Referenzusers. Plan-Mode darf keine Side-Effects haben — nur Read-Calls.
internal interface IReferenceUserDirectoryReader
{
    Task<ReferenceUserDirectoryResult> LoadGroupsAsync(string userPrincipalName, CancellationToken cancellationToken);
}

internal sealed record ReferenceUserGroupDto(string GroupId, string DisplayName, string? OnPremDistinguishedName);

internal abstract record ReferenceUserDirectoryResult
{
    public sealed record Loaded(string DisplayName, IReadOnlyList<ReferenceUserGroupDto> Groups) : ReferenceUserDirectoryResult;
    public sealed record NotFound(string UserPrincipalName) : ReferenceUserDirectoryResult;
    public sealed record PermissionMissing(string Message) : ReferenceUserDirectoryResult;
    public sealed record TransientFailure(string Message) : ReferenceUserDirectoryResult;
}

// Cache-Entry, der vom Pre-Loader in PostgresWorkflowAutomationOperations gebaut wird.
// Resolver-Case 'reference_user' liest aus diesem Dict (keyed by answerKey).
internal sealed record ReferenceUserGroupsCacheEntry(
    bool IsSuccess,
    string? ErrorMessage,
    string? DisplayName,
    IReadOnlyList<ReferenceUserGroupDto> Groups)
{
    public static ReferenceUserGroupsCacheEntry FromLoaded(string displayName, IReadOnlyList<ReferenceUserGroupDto> groups)
        => new(true, null, displayName, groups);

    public static ReferenceUserGroupsCacheEntry FromError(string message)
        => new(false, message, null, Array.Empty<ReferenceUserGroupDto>());
}
