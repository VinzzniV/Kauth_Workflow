namespace API;

internal static class AuthorizationRoles
{
    public const string SystemRoleKind = "system";
    public const string Hr = "auth_hr";
    public const string Manager = "auth_manager";
    public const string Worker = "auth_worker";
    public const string Admin = "auth_admin";
    public const string Reader = "auth_reader";

    public static readonly string[] ReadAllowed =
    {
        Hr,
        Manager,
        Worker,
        Admin,
        Reader
    };
}
