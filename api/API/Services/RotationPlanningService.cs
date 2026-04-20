using Microsoft.Extensions.Logging;

namespace API;

internal sealed class RotationPlanningService(
    IRotationRepository rotationRepository,
    IRotationTaskGenerationService rotationTaskGenerationService,
    IWorkflowVisibilityService workflowVisibilityService,
    ILogger<RotationPlanningService> logger) : IRotationPlanningService
{
    private static readonly HashSet<string> SupportedPlanStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft",
        "active",
        "completed",
        "archived"
    };

    private static readonly HashSet<string> SupportedStationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "planned",
        "active",
        "completed",
        "cancelled"
    };

    public async Task<IReadOnlyList<RotationPlanListItemDto>> GetRotationPlansAsync(
        long? personId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (personId.HasValue && personId.Value <= 0)
        {
            throw new InvalidOperationException("personId must be greater than zero.");
        }

        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        return await rotationRepository.GetRotationPlans(personId, observableDepartmentIds);
    }

    public async Task<RotationPlanDetailDto?> GetRotationPlanAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (planId <= 0)
        {
            throw new InvalidOperationException("planId must be greater than zero.");
        }

        var plan = await rotationRepository.GetRotationPlan(planId);
        if (plan is null)
        {
            return null;
        }

        await EnsureVisibleAsync(plan.DepartmentId, currentUser);
        return plan;
    }

    public async Task<IReadOnlyList<RotationAuditEntryDto>?> GetRotationAuditLogAsync(
        long planId,
        int limit,
        int offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetRotationPlanAsync(planId, currentUser, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        return await rotationRepository.GetRotationAuditLog(planId, limit, offset);
    }

    public async Task<IReadOnlyList<RotationNotificationDto>?> GetRotationNotificationsAsync(
        long planId,
        int limit,
        int offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetRotationPlanAsync(planId, currentUser, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        return await rotationRepository.GetRotationNotifications(planId, limit, offset);
    }

    public async Task<RotationPlanDetailDto> CreateRotationPlanAsync(
        CreateRotationPlanRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PersonId <= 0)
        {
            throw new InvalidOperationException("personId must be greater than zero.");
        }

        if (request.SourceWorkflowUid == Guid.Empty)
        {
            throw new InvalidOperationException("sourceWorkflowUid is required.");
        }

        var source = await rotationRepository.GetCompletedOnboardingSource(request.SourceWorkflowUid);
        if (source is null)
        {
            throw new InvalidOperationException("Das angegebene abgeschlossene Onboarding wurde nicht gefunden.");
        }

        await EnsureVisibleAsync(source.DepartmentId, currentUser);

        if (source.PersonId != request.PersonId)
        {
            throw new InvalidOperationException(
                "Der Durchlaufplan muss auf dieselbe Person wie der abgeschlossene Onboarding-Vorgang referenzieren.");
        }

        var normalizedStatus = NormalizePlanStatus(request.Status);
        var conflictState = await rotationRepository.GetRotationPlanConflictState(request.PersonId, request.SourceWorkflowUid);
        if (normalizedStatus == "active" && conflictState.HasActivePlanForPerson)
        {
            throw new InvalidOperationException("Für diese Person existiert bereits ein aktiver Durchlaufplan.");
        }

        if (conflictState.HasOpenPlanForSourceWorkflow)
        {
            throw new InvalidOperationException(
                "Für dieses abgeschlossene Onboarding existiert bereits ein offener Durchlaufplan.");
        }

        var persistedRequest = new CreateRotationPlanRequest
        {
            PersonId = request.PersonId,
            SourceWorkflowUid = request.SourceWorkflowUid,
            Title = NormalizePlanTitle(request.Title, source.DisplayName),
            Status = normalizedStatus
        };

        var createdPlan = await rotationRepository.CreateRotationPlan(persistedRequest, currentUser.UserId);
        await rotationTaskGenerationService.RegeneratePlanTasksUncheckedAsync(
            createdPlan.Id,
            currentUser,
            "plan_created",
            cancellationToken);
        logger.LogInformation(
            "Rotation plan {RotationPlanId} created by user {UserId} for person {PersonId} from workflow {SourceWorkflowUid}.",
            createdPlan.Id,
            currentUser.UserId,
            createdPlan.PersonId,
            createdPlan.SourceWorkflowUid);
        return createdPlan;
    }

    public async Task<IReadOnlyList<RotationStationDto>?> GetRotationStationsAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetRotationPlanAsync(planId, currentUser, cancellationToken);
        return plan?.Stations;
    }

    public async Task<RotationStationDto?> CreateRotationStationAsync(
        long planId,
        RotationStationUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await GetRotationPlanAsync(planId, currentUser, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        EnsurePlanAllowsStationChanges(plan);
        var normalizedRequest = NormalizeStationRequest(request);
        ValidateStationConflicts(plan.Stations, null, normalizedRequest);

        var station = await rotationRepository.CreateRotationStation(planId, normalizedRequest, currentUser.UserId);
        if (station is not null)
        {
            await rotationTaskGenerationService.RegeneratePlanTasksUncheckedAsync(
                planId,
                currentUser,
                "station_created",
                cancellationToken);
            logger.LogInformation(
                "Rotation station {RotationStationId} created by user {UserId} for plan {RotationPlanId}.",
                station.Id,
                currentUser.UserId,
                planId);
        }

        return station;
    }

    public async Task<RotationStationDto?> UpdateRotationStationAsync(
        long stationId,
        RotationStationUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (stationId <= 0)
        {
            throw new InvalidOperationException("stationId must be greater than zero.");
        }

        var currentStation = await rotationRepository.GetRotationStation(stationId);
        if (currentStation is null)
        {
            return null;
        }

        var plan = await GetRotationPlanAsync(currentStation.RotationPlanId, currentUser, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        EnsurePlanAllowsStationChanges(plan);
        var normalizedRequest = NormalizeStationRequest(request);
        ValidateStationConflicts(plan.Stations, stationId, normalizedRequest);

        var station = await rotationRepository.UpdateRotationStation(stationId, normalizedRequest, currentUser.UserId);
        if (station is not null)
        {
            await rotationTaskGenerationService.RegeneratePlanTasksUncheckedAsync(
                plan.Id,
                currentUser,
                "station_updated",
                cancellationToken);
            logger.LogInformation(
                "Rotation station {RotationStationId} updated by user {UserId}.",
                stationId,
                currentUser.UserId);
        }

        return station;
    }

    public async Task<bool> DeleteRotationStationAsync(
        long stationId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (stationId <= 0)
        {
            throw new InvalidOperationException("stationId must be greater than zero.");
        }

        var currentStation = await rotationRepository.GetRotationStation(stationId);
        if (currentStation is null)
        {
            return false;
        }

        var plan = await GetRotationPlanAsync(currentStation.RotationPlanId, currentUser, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        EnsurePlanAllowsStationChanges(plan);
        var deleted = await rotationRepository.DeleteRotationStation(stationId, currentUser.UserId);
        if (deleted)
        {
            await rotationTaskGenerationService.RegeneratePlanTasksUncheckedAsync(
                plan.Id,
                currentUser,
                "station_deleted",
                cancellationToken);
            logger.LogInformation(
                "Rotation station {RotationStationId} deleted by user {UserId}.",
                stationId,
                currentUser.UserId);
        }

        return deleted;
    }

    private async Task EnsureVisibleAsync(int? departmentId, CurrentUser currentUser)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (observableDepartmentIds is null)
        {
            return;
        }

        if (!departmentId.HasValue || !observableDepartmentIds.Contains(departmentId.Value))
        {
            throw new UnauthorizedAccessException("Der Durchlaufplan liegt außerhalb Ihrer freigegebenen Abteilungen.");
        }
    }

    private static string NormalizePlanStatus(string? status)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? "draft"
            : status.Trim().ToLowerInvariant();

        if (!SupportedPlanStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException($"status '{status}' is not supported.");
        }

        return normalizedStatus;
    }

    private static string NormalizePlanTitle(string? title, string displayName)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? $"{displayName} - Durchlaufplan"
            : title.Trim();

        if (normalizedTitle.Length > 220)
        {
            throw new InvalidOperationException("title darf maximal 220 Zeichen haben.");
        }

        return normalizedTitle;
    }

    private static RotationStationUpsertRequest NormalizeStationRequest(RotationStationUpsertRequest request)
    {
        if (request.DepartmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        if (request.EndDate < request.StartDate)
        {
            throw new InvalidOperationException("endDate must be greater than or equal to startDate.");
        }

        if (request.OrderIndex < 0)
        {
            throw new InvalidOperationException("orderIndex must be greater than or equal to zero.");
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? "planned"
            : request.Status.Trim().ToLowerInvariant();
        if (!SupportedStationStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException($"status '{request.Status}' is not supported.");
        }

        var normalizedLocation = string.IsNullOrWhiteSpace(request.Location)
            ? null
            : request.Location.Trim();
        if (normalizedLocation?.Length > 160)
        {
            throw new InvalidOperationException("location darf maximal 160 Zeichen haben.");
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        return new RotationStationUpsertRequest
        {
            DepartmentId = request.DepartmentId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            OrderIndex = request.OrderIndex,
            Location = normalizedLocation,
            Notes = normalizedNotes,
            Status = normalizedStatus
        };
    }

    private static void ValidateStationConflicts(
        IReadOnlyList<RotationStationDto> existingStations,
        long? currentStationId,
        RotationStationUpsertRequest request)
    {
        var conflictingOrder = existingStations.FirstOrDefault(station =>
            station.Id != currentStationId
            && station.OrderIndex == request.OrderIndex);
        if (conflictingOrder is not null)
        {
            throw new InvalidOperationException(
                $"orderIndex {request.OrderIndex} wird im Durchlaufplan bereits verwendet.");
        }

        var conflictingDateRange = existingStations.FirstOrDefault(station =>
            station.Id != currentStationId
            && station.StartDate <= request.EndDate
            && request.StartDate <= station.EndDate);
        if (conflictingDateRange is not null)
        {
            throw new InvalidOperationException(
                $"Die Station überschneidet sich mit der bestehenden Station {conflictingDateRange.Id}.");
        }
    }

    private static void EnsurePlanAllowsStationChanges(RotationPlanDetailDto plan)
    {
        if (plan.Status is "completed" or "archived")
        {
            throw new InvalidOperationException(
                "Stationen koennen nur in Durchlaufplaenen mit Status draft oder active geaendert werden.");
        }
    }
}
