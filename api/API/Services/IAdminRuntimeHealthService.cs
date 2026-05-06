namespace API;

internal interface IAdminRuntimeHealthService
{
    Task<AdminRuntimeHealthDto> GetRuntimeHealthAsync(CancellationToken cancellationToken = default);
}
