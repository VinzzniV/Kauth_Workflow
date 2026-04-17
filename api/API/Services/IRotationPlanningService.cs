namespace API;

internal interface IRotationPlanningService
{
    Task<IReadOnlyList<RotationPlanListItemDto>> GetRotationPlansAsync(
        long? personId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<RotationPlanDetailDto?> GetRotationPlanAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RotationAuditEntryDto>?> GetRotationAuditLogAsync(
        long planId,
        int limit,
        int offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RotationNotificationDto>?> GetRotationNotificationsAsync(
        long planId,
        int limit,
        int offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<RotationPlanDetailDto> CreateRotationPlanAsync(
        CreateRotationPlanRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RotationStationDto>?> GetRotationStationsAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<RotationStationDto?> CreateRotationStationAsync(
        long planId,
        RotationStationUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<RotationStationDto?> UpdateRotationStationAsync(
        long stationId,
        RotationStationUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteRotationStationAsync(
        long stationId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
}
