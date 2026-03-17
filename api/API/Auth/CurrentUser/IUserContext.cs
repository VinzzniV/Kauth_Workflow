namespace API;

public interface IUserContext
{
    // Returns the current application user (resolved identity + role/group model).
    Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default);
}
