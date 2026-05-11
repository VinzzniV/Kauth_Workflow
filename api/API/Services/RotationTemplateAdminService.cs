using Microsoft.Extensions.Logging;

namespace API;

internal sealed class RotationTemplateAdminService(
    IRotationRepository rotationRepository,
    IRotationTaskGenerationService rotationTaskGenerationService,
    IWorkflowAutomationHandlerRegistry automationHandlerRegistry,
    ILogger<RotationTemplateAdminService> logger) : IRotationTemplateAdminService
{
    private static readonly HashSet<string> SupportedTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        RotationTriggerTypes.Enter,
        RotationTriggerTypes.Exit
    };

    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        RotationTaskTypes.Manual,
        RotationTaskTypes.Technical,
        RotationTaskTypes.Approval,
        RotationTaskTypes.Information
    };

    public async Task<IReadOnlyList<DepartmentActionTemplateDto>> GetDepartmentActionTemplatesAsync(
        int? departmentId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var page = await GetDepartmentActionTemplatesAsync(
            departmentId,
            isActive,
            new AdminListQuery { Limit = AdminListQuery.MaxLimit, Offset = 0 },
            cancellationToken);
        return page.Items;
    }

    public async Task<AdminListPageDto<DepartmentActionTemplateDto>> GetDepartmentActionTemplatesAsync(
        int? departmentId,
        bool? isActive,
        AdminListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (departmentId is <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var templates = await rotationRepository.GetDepartmentActionTemplates(departmentId, isActive);
        var search = query.NormalizedSearch;
        var filtered = templates
            .Where(template =>
                string.IsNullOrWhiteSpace(search)
                || template.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (template.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || template.TriggerType.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.TaskType.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (template.AutomationKey?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(template => template.DepartmentName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(template => template.TriggerType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(template => template.Id)
            .ToList();

        return AdminListPage.From(filtered, query);
    }

    public async Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplateAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        return await rotationRepository.GetDepartmentActionTemplate(templateId);
    }

    public async Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplateAsync(
        DepartmentActionTemplateUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = await NormalizeTemplateRequestAsync(request);
        var template = await rotationRepository.CreateDepartmentActionTemplate(normalizedRequest);
        await rotationTaskGenerationService.RegenerateDepartmentPlansAsync(
            template.DepartmentId,
            currentUser,
            "template_created",
            cancellationToken);

        logger.LogInformation(
            "Department action template {TemplateId} created by user {UserId} for department {DepartmentId}.",
            template.Id,
            currentUser.UserId,
            template.DepartmentId);

        return template;
    }

    public async Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplateAsync(
        int templateId,
        DepartmentActionTemplateUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        var currentTemplate = await rotationRepository.GetDepartmentActionTemplate(templateId);
        if (currentTemplate is null)
        {
            return null;
        }

        var normalizedRequest = await NormalizeTemplateRequestAsync(request);
        var updatedTemplate = await rotationRepository.UpdateDepartmentActionTemplate(templateId, normalizedRequest);
        if (updatedTemplate is not null)
        {
            if (currentTemplate.DepartmentId != updatedTemplate.DepartmentId)
            {
                await rotationTaskGenerationService.RegenerateDepartmentPlansAsync(
                    currentTemplate.DepartmentId,
                    currentUser,
                    "template_updated",
                    cancellationToken);
            }

            await rotationTaskGenerationService.RegenerateDepartmentPlansAsync(
                updatedTemplate.DepartmentId,
                currentUser,
                "template_updated",
                cancellationToken);
            logger.LogInformation(
                "Department action template {TemplateId} updated by user {UserId}.",
                templateId,
                currentUser.UserId);
        }

        return updatedTemplate;
    }

    public async Task<bool> DeleteDepartmentActionTemplateAsync(
        int templateId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        var currentTemplate = await rotationRepository.GetDepartmentActionTemplate(templateId);
        if (currentTemplate is null)
        {
            return false;
        }

        var deleted = await rotationRepository.DeleteDepartmentActionTemplate(templateId);
        if (deleted)
        {
            await rotationTaskGenerationService.RegenerateDepartmentPlansAsync(
                currentTemplate.DepartmentId,
                currentUser,
                "template_deactivated",
                cancellationToken);
            logger.LogInformation(
                "Department action template {TemplateId} deactivated by user {UserId}.",
                templateId,
                currentUser.UserId);
        }

        return deleted;
    }

    private async Task<DepartmentActionTemplateUpsertRequest> NormalizeTemplateRequestAsync(
        DepartmentActionTemplateUpsertRequest request)
    {
        if (request.DepartmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        if (!await rotationRepository.DepartmentExists(request.DepartmentId))
        {
            throw new InvalidOperationException("Die angegebene Abteilung existiert nicht.");
        }

        if (!request.DefaultResponsibilityId.HasValue)
        {
            throw new InvalidOperationException("defaultResponsibilityId ist erforderlich. Aufgaben ohne Zuständigkeit sind für Fachbereiche nicht sichtbar.");
        }

        if (request.DefaultResponsibilityId is <= 0)
        {
            throw new InvalidOperationException("defaultResponsibilityId must be greater than zero when provided.");
        }

        if (request.DefaultResponsibilityId.HasValue
            && !await rotationRepository.ResponsibilityExists(request.DefaultResponsibilityId.Value))
        {
            throw new InvalidOperationException("Die angegebene Verantwortlichkeit existiert nicht.");
        }

        var normalizedTriggerType = NormalizeTriggerType(request.TriggerType);
        var normalizedTaskType = NormalizeTaskType(request.TaskType);
        var normalizedTitle = NormalizeTitle(request.Title);
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var normalizedAutomationKey = ValidateAndNormalizeAutomationKey(request.IsAutomatable, request.AutomationKey);

        if (request.DueOffsetDays < -365 || request.DueOffsetDays > 365)
        {
            throw new InvalidOperationException("dueOffsetDays muss zwischen -365 und 365 liegen.");
        }

        if (request.ReminderOffsetDays is < 0)
        {
            throw new InvalidOperationException("reminderOffsetDays must be greater than or equal to zero.");
        }

        return new DepartmentActionTemplateUpsertRequest
        {
            DepartmentId = request.DepartmentId,
            TriggerType = normalizedTriggerType,
            Title = normalizedTitle,
            Description = normalizedDescription,
            TaskType = normalizedTaskType,
            DefaultResponsibilityId = request.DefaultResponsibilityId,
            DueOffsetDays = request.DueOffsetDays,
            ReminderOffsetDays = request.ReminderOffsetDays,
            IsAutomatable = request.IsAutomatable,
            AutomationKey = normalizedAutomationKey,
            IsActive = request.IsActive
        };
    }

    private static string NormalizeTriggerType(string? triggerType)
    {
        var normalizedValue = triggerType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedValue) || !SupportedTriggerTypes.Contains(normalizedValue))
        {
            throw new InvalidOperationException($"triggerType '{triggerType}' is not supported.");
        }

        return normalizedValue;
    }

    private static string NormalizeTaskType(string? taskType)
    {
        var normalizedValue = taskType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedValue) || !SupportedTaskTypes.Contains(normalizedValue))
        {
            throw new InvalidOperationException($"taskType '{taskType}' is not supported.");
        }

        return normalizedValue;
    }

    private static string NormalizeTitle(string? title)
    {
        var normalizedValue = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new InvalidOperationException("title is required.");
        }

        if (normalizedValue.Length > 220)
        {
            throw new InvalidOperationException("title darf maximal 220 Zeichen haben.");
        }

        return normalizedValue;
    }

    private string? ValidateAndNormalizeAutomationKey(bool isAutomatable, string? automationKey)
    {
        var normalizedValue = string.IsNullOrWhiteSpace(automationKey)
            ? null
            : automationKey.Trim();

        if (!isAutomatable && normalizedValue is not null)
        {
            throw new InvalidOperationException(
                "automationKey darf nur gesetzt sein, wenn die Vorlage als automatable markiert ist.");
        }

        if (normalizedValue is not null && normalizedValue.Length > 120)
        {
            throw new InvalidOperationException("automationKey darf maximal 120 Zeichen haben.");
        }

        if (isAutomatable && normalizedValue is null)
        {
            throw new InvalidOperationException(
                "automationKey ist erforderlich, wenn die Vorlage als automatable markiert ist.");
        }

        if (normalizedValue is not null)
        {
            var registeredKeys = automationHandlerRegistry.GetRegisteredKeys();
            if (!registeredKeys.Contains(normalizedValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unbekannter automationKey '{normalizedValue}'.");
            }
        }

        return normalizedValue;
    }
}
