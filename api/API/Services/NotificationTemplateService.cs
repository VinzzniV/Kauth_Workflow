namespace API;

internal sealed class NotificationTemplateService(
    INotificationTemplateRepository repository,
    INotificationTemplatePreviewRepository previewRepository,
    IRotationNotificationPreviewRepository rotationPreviewRepository,
    IWorkflowRepository workflowRepository,
    IRotationRepository rotationRepository,
    INotificationEmailConfigurationService notificationEmailConfigurationService) : INotificationTemplateService
{
    public async Task<IReadOnlyList<AdminNotificationTemplateDto>> GetAdminTemplates(CancellationToken cancellationToken = default)
    {
        var page = await GetAdminTemplates(
            new AdminListQuery { Limit = AdminListQuery.MaxLimit, Offset = 0 },
            cancellationToken);
        return page.Items;
    }

    public async Task<AdminListPageDto<AdminNotificationTemplateDto>> GetAdminTemplates(
        AdminListQuery query,
        CancellationToken cancellationToken = default)
    {
        var storedTemplates = await repository.GetTemplates(cancellationToken);
        var search = query.NormalizedSearch;
        var templates = NotificationTemplateCatalog
            .GetDefinitions()
            .Select(definition => MapToAdminDto(definition, FindStoredTemplate(storedTemplates, definition.TemplateKey)))
            .Where(template =>
                string.IsNullOrWhiteSpace(search)
                || template.TemplateKey.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.TriggerDescription.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.PreviewTargetType.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(template => template.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(template => template.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return AdminListPage.From(templates, query);
    }

    public async Task<AdminNotificationTemplateDto> UpdateAdminTemplate(
        string templateKey,
        AdminNotificationTemplateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = NotificationTemplateCatalog.GetDefinitionOrThrow(templateKey);
        var normalizedSubject = NormalizeTemplateText(request.SubjectTemplate, "subjectTemplate");
        var normalizedBody = NormalizeTemplateText(request.BodyTemplate, "bodyTemplate");
        ValidatePlaceholders(definition, normalizedSubject, normalizedBody);

        var stored = await repository.UpsertTemplate(
            new NotificationTemplateUpsertModel
            {
                TemplateKey = definition.TemplateKey,
                DisplayName = definition.DisplayName,
                TriggerDescription = definition.TriggerDescription,
                SubjectTemplate = normalizedSubject,
                BodyTemplate = normalizedBody,
                IsSystemLocked = false
            },
            cancellationToken);

        return MapToAdminDto(definition, stored);
    }

    public async Task<IReadOnlyList<AdminNotificationTemplateWorkflowPreviewTargetDto>> SearchWorkflowPreviewTargets(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 50);
        var result = await workflowRepository.GetFilteredWorkflows(new WorkflowListQuery
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant(),
            Limit = effectiveLimit,
            Offset = 0,
            ReaderOnly = false,
            IncludeFilterOptions = false
        });

        return result.Items
            .Select(item => new AdminNotificationTemplateWorkflowPreviewTargetDto
            {
                WorkflowUid = item.Uid,
                DisplayName = BuildWorkflowDisplayName(item),
                ProcessName = item.WorkflowDefinition.Name,
                DepartmentName = item.DepartmentName,
                WorkflowStatus = item.WorkflowStatus,
                CreatedAt = item.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AdminNotificationTemplateRotationPlanPreviewTargetDto>> SearchRotationPlanPreviewTargets(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 50);
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToLowerInvariant();

        var plans = await rotationRepository.GetRotationPlans(null, null);
        return plans
            .Where(plan =>
                normalizedSearch is null
                || $"{plan.Title} {plan.DisplayName} {plan.DepartmentName ?? string.Empty} {plan.SourceWorkflowUid}"
                    .ToLowerInvariant()
                    .Contains(normalizedSearch, StringComparison.Ordinal))
            .OrderByDescending(plan => plan.UpdatedAt)
            .Take(effectiveLimit)
            .Select(plan => new AdminNotificationTemplateRotationPlanPreviewTargetDto
            {
                RotationPlanId = plan.Id,
                Title = plan.Title,
                DisplayName = plan.DisplayName,
                DepartmentName = plan.DepartmentName,
                Status = plan.Status,
                UpdatedAt = plan.UpdatedAt
            })
            .ToList();
    }

    public async Task<AdminNotificationTemplatePreviewResponseDto> BuildPreview(
        string templateKey,
        AdminNotificationTemplatePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = NotificationTemplateCatalog.GetDefinitionOrThrow(templateKey);
        var selectedTarget = await ResolveSelectedTarget(definition, request, cancellationToken);
        var frontendConfiguration = await notificationEmailConfigurationService.GetRuntimeConfiguration(cancellationToken);

        if (string.Equals(definition.PreviewTargetType, NotificationTemplatePreviewTargetTypes.Workflow, StringComparison.OrdinalIgnoreCase))
        {
            var workflowUid = request.WorkflowUid
                ?? throw new InvalidOperationException("workflowUid ist für diese Preview erforderlich.");
            return await BuildWorkflowPreview(definition, workflowUid, selectedTarget, frontendConfiguration.FrontendBaseUrl, cancellationToken);
        }

        var rotationPlanId = request.RotationPlanId
            ?? throw new InvalidOperationException("rotationPlanId ist für diese Preview erforderlich.");
        return await BuildRotationPreview(definition, rotationPlanId, selectedTarget, frontendConfiguration.FrontendBaseUrl, cancellationToken);
    }

    public async Task<NotificationTemplateRenderResult> RenderWorkflowNotification(
        WorkflowNotificationRenderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definition = NotificationTemplateCatalog.GetDefinitionOrThrow(context.TemplateKey);
        var template = await GetEffectiveTemplate(definition, cancellationToken);
        var processTypeContext = NotificationEmailTemplateBuilder.ResolveProcessTypeEmailContext(
            context.WorkflowDefinitionKey,
            context.WorkflowDefinitionName);
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipient_name"] = context.RecipientName.Trim(),
            ["process_name"] = processTypeContext.Name,
            ["workflow_label"] = processTypeContext.WorkflowLabel,
            ["process_label"] = processTypeContext.ProcessLabel,
            ["task_list_text"] = BuildWorkflowTaskListText(context.TaskTitles, processTypeContext.ProcessLabel),
            ["workflow_url"] = context.WorkflowUrl.Trim(),
        };

        var built = NotificationEmailTemplateBuilder.Build(
            template.SubjectTemplate,
            template.BodyTemplate,
            placeholders,
            context.RecipientName,
            context.WorkflowUrl,
            definition.ActionLabel);

        return new NotificationTemplateRenderResult
        {
            Subject = built.Subject,
            TextBody = built.TextBody,
            HtmlBody = built.HtmlBody,
            PlaceholderValues = built.PlaceholderValues
        };
    }

    public async Task<NotificationTemplateRenderResult> RenderRotationNotification(
        RotationNotificationRenderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definition = NotificationTemplateCatalog.GetDefinitionOrThrow(context.TemplateKey);
        var template = await GetEffectiveTemplate(definition, cancellationToken);
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipient_name"] = context.RecipientName.Trim(),
            ["person_name"] = context.Payload.PersonDisplayName,
            ["plan_title"] = context.Payload.PlanTitle,
            ["current_department_name"] = NormalizeRotationPlaceholder(context.Payload.CurrentDepartmentName),
            ["next_department_name"] = NormalizeRotationPlaceholder(context.Payload.NextDepartmentName),
            ["change_date"] = context.Payload.ChangeDate?.ToString("dd.MM.yyyy") ?? "n. a.",
            ["task_list_text"] = BuildRotationTaskListText(context.Payload.Tasks),
            ["app_url"] = context.AppUrl.Trim(),
        };

        var built = NotificationEmailTemplateBuilder.Build(
            template.SubjectTemplate,
            template.BodyTemplate,
            placeholders,
            context.RecipientName,
            context.AppUrl,
            definition.ActionLabel);

        return new NotificationTemplateRenderResult
        {
            Subject = built.Subject,
            TextBody = built.TextBody,
            HtmlBody = built.HtmlBody,
            PlaceholderValues = built.PlaceholderValues
        };
    }

    private async Task<AdminNotificationTemplatePreviewResponseDto> BuildWorkflowPreview(
        NotificationTemplateDefinition definition,
        Guid workflowUid,
        AdminNotificationTemplatePreviewTargetDto selectedTarget,
        string frontendBaseUrl,
        CancellationToken cancellationToken)
    {
        var targets = definition.TemplateKey switch
        {
            NotificationTemplateKeys.WorkflowCreated => await previewRepository.GetWorkflowCreatedPreviewTargets(workflowUid),
            NotificationTemplateKeys.TaskReady => await previewRepository.GetTaskReadyPreviewTargets(workflowUid),
            NotificationTemplateKeys.WorkflowCompleted => await previewRepository.GetWorkflowCompletedPreviewTargets(workflowUid),
            _ => []
        };

        if (targets.Count == 0)
        {
            return CreateBlockedPreviewResponse(definition, selectedTarget, GetWorkflowBlockingReason(definition.TemplateKey));
        }

        var variants = new List<AdminNotificationTemplatePreviewVariantDto>();
        foreach (var batch in GroupWorkflowTargets(targets))
        {
            var rendered = await RenderWorkflowNotification(
                new WorkflowNotificationRenderContext
                {
                    TemplateKey = definition.TemplateKey,
                    RecipientName = batch.PrimaryTarget.TargetName,
                    WorkflowUrl = BuildWorkflowAccessUrl(frontendBaseUrl, workflowUid, batch),
                    WorkflowDefinitionKey = batch.PrimaryTarget.WorkflowDefinitionKey,
                    WorkflowDefinitionName = batch.PrimaryTarget.WorkflowDefinitionName,
                    TaskTitles = batch.TaskTitles
                },
                cancellationToken);

            variants.Add(new AdminNotificationTemplatePreviewVariantDto
            {
                Recipient = new AdminNotificationTemplatePreviewRecipientDto
                {
                    RecipientUserId = batch.PrimaryTarget.RecipientUserId,
                    Name = batch.PrimaryTarget.TargetName,
                    Email = batch.PrimaryTarget.TargetEmail
                },
                RenderedSubject = rendered.Subject,
                RenderedTextBody = rendered.TextBody,
                RenderedHtmlBody = rendered.HtmlBody,
                PlaceholderValues = rendered.PlaceholderValues
                    .Select(entry => new AdminNotificationTemplatePlaceholderValueDto
                    {
                        Key = entry.Key,
                        Value = entry.Value
                    })
                    .ToList()
            });
        }

        return new AdminNotificationTemplatePreviewResponseDto
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            PreviewTargetType = definition.PreviewTargetType,
            IsCurrentlyTriggerable = true,
            BlockingReason = null,
            Target = selectedTarget,
            Variants = variants
        };
    }

    private async Task<AdminNotificationTemplatePreviewResponseDto> BuildRotationPreview(
        NotificationTemplateDefinition definition,
        long rotationPlanId,
        AdminNotificationTemplatePreviewTargetDto selectedTarget,
        string frontendBaseUrl,
        CancellationToken cancellationToken)
    {
        var targets = await rotationPreviewRepository.GetRotationNotificationPreviewTargets(
            rotationPlanId,
            definition.TemplateKey,
            DateOnly.FromDateTime(DateTime.UtcNow));
        if (targets.Count == 0)
        {
            return CreateBlockedPreviewResponse(definition, selectedTarget, GetRotationBlockingReason(definition.TemplateKey));
        }

        var variants = new List<AdminNotificationTemplatePreviewVariantDto>(targets.Count);
        foreach (var target in targets)
        {
            var appUrl = BuildRotationAccessUrl(frontendBaseUrl, target.Payload.LinkPath);
            var rendered = await RenderRotationNotification(
                new RotationNotificationRenderContext
                {
                    TemplateKey = definition.TemplateKey,
                    RecipientName = target.TargetName,
                    AppUrl = appUrl,
                    Payload = target.Payload
                },
                cancellationToken);

            variants.Add(new AdminNotificationTemplatePreviewVariantDto
            {
                Recipient = new AdminNotificationTemplatePreviewRecipientDto
                {
                    RecipientUserId = target.RecipientUserId,
                    Name = target.TargetName,
                    Email = target.TargetEmail
                },
                RenderedSubject = rendered.Subject,
                RenderedTextBody = rendered.TextBody,
                RenderedHtmlBody = rendered.HtmlBody,
                PlaceholderValues = rendered.PlaceholderValues
                    .Select(entry => new AdminNotificationTemplatePlaceholderValueDto
                    {
                        Key = entry.Key,
                        Value = entry.Value
                    })
                    .ToList()
            });
        }

        return new AdminNotificationTemplatePreviewResponseDto
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            PreviewTargetType = definition.PreviewTargetType,
            IsCurrentlyTriggerable = true,
            BlockingReason = null,
            Target = selectedTarget,
            Variants = variants
        };
    }

    private async Task<AdminNotificationTemplatePreviewTargetDto> ResolveSelectedTarget(
        NotificationTemplateDefinition definition,
        AdminNotificationTemplatePreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(definition.PreviewTargetType, NotificationTemplatePreviewTargetTypes.Workflow, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.WorkflowUid.HasValue)
            {
                throw new InvalidOperationException("workflowUid ist für diese Preview erforderlich.");
            }

            var workflow = await workflowRepository.GetWorkflowByUid(request.WorkflowUid.Value);
            if (workflow is null)
            {
                throw new InvalidOperationException("Der ausgewählte Workflow wurde nicht gefunden.");
            }

            return new AdminNotificationTemplatePreviewTargetDto
            {
                TargetType = NotificationTemplatePreviewTargetTypes.Workflow,
                WorkflowUid = workflow.Uid,
                RotationPlanId = null,
                PrimaryLabel = BuildWorkflowDisplayName(workflow),
                SecondaryLabel = $"{workflow.WorkflowDefinition.Name} | {workflow.DepartmentName}",
                Status = workflow.WorkflowStatus
            };
        }

        if (!request.RotationPlanId.HasValue)
        {
            throw new InvalidOperationException("rotationPlanId ist für diese Preview erforderlich.");
        }

        var plan = await rotationRepository.GetRotationPlan(request.RotationPlanId.Value);
        if (plan is null)
        {
            throw new InvalidOperationException("Der ausgewählte Durchlaufplan wurde nicht gefunden.");
        }

        return new AdminNotificationTemplatePreviewTargetDto
        {
            TargetType = NotificationTemplatePreviewTargetTypes.RotationPlan,
            WorkflowUid = null,
            RotationPlanId = plan.Id,
            PrimaryLabel = plan.Title,
            SecondaryLabel = $"{plan.DisplayName} | {plan.DepartmentName ?? "ohne Bereich"}",
            Status = plan.Status
        };
    }

    private static AdminNotificationTemplatePreviewResponseDto CreateBlockedPreviewResponse(
        NotificationTemplateDefinition definition,
        AdminNotificationTemplatePreviewTargetDto selectedTarget,
        string blockingReason)
    {
        return new AdminNotificationTemplatePreviewResponseDto
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            PreviewTargetType = definition.PreviewTargetType,
            IsCurrentlyTriggerable = false,
            BlockingReason = blockingReason,
            Target = selectedTarget,
            Variants = []
        };
    }

    private async Task<StoredNotificationTemplate> GetEffectiveTemplate(
        NotificationTemplateDefinition definition,
        CancellationToken cancellationToken)
    {
        var stored = await repository.GetTemplate(definition.TemplateKey, cancellationToken);
        if (stored is not null)
        {
            return stored;
        }

        return new StoredNotificationTemplate
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            SubjectTemplate = definition.DefaultSubjectTemplate,
            BodyTemplate = definition.DefaultBodyTemplate,
            IsSystemLocked = false,
            UpdatedAt = null
        };
    }

    private static StoredNotificationTemplate? FindStoredTemplate(
        IEnumerable<StoredNotificationTemplate> storedTemplates,
        string templateKey)
    {
        return storedTemplates.FirstOrDefault(template =>
            string.Equals(template.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase));
    }

    private static AdminNotificationTemplateDto MapToAdminDto(
        NotificationTemplateDefinition definition,
        StoredNotificationTemplate? stored)
    {
        var effective = stored ?? new StoredNotificationTemplate
        {
            TemplateKey = definition.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            SubjectTemplate = definition.DefaultSubjectTemplate,
            BodyTemplate = definition.DefaultBodyTemplate,
            IsSystemLocked = false,
            UpdatedAt = null
        };

        return new AdminNotificationTemplateDto
        {
            TemplateKey = effective.TemplateKey,
            DisplayName = definition.DisplayName,
            TriggerDescription = definition.TriggerDescription,
            SubjectTemplate = effective.SubjectTemplate,
            BodyTemplate = effective.BodyTemplate,
            IsSystemLocked = effective.IsSystemLocked,
            UpdatedAt = effective.UpdatedAt,
            PreviewTargetType = definition.PreviewTargetType,
            Placeholders = definition.Placeholders.ToList()
        };
    }

    private static void ValidatePlaceholders(
        NotificationTemplateDefinition definition,
        string subjectTemplate,
        string bodyTemplate)
    {
        var allowedKeys = definition.Placeholders
            .Select(placeholder => placeholder.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedKeys = NotificationEmailTemplateBuilder.ExtractPlaceholderKeys(subjectTemplate)
            .Concat(NotificationEmailTemplateBuilder.ExtractPlaceholderKeys(bodyTemplate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownKeys = usedKeys
            .Where(key => !allowedKeys.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknownKeys.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unbekannte Platzhalter für '{definition.DisplayName}': {string.Join(", ", unknownKeys.Select(key => "{{" + key + "}}"))}.");
    }

    private static string NormalizeTemplateText(string? value, string fieldName)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"{fieldName} ist erforderlich.");
        }

        var normalized = value.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} darf nicht leer sein.");
        }

        return normalized;
    }

    private static string BuildWorkflowTaskListText(IReadOnlyList<string> taskTitles, string processLabel)
    {
        if (taskTitles.Count == 0)
        {
            return $"- Aufgaben im {processLabel}";
        }

        return string.Join(
            "\n",
            taskTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(title => title, StringComparer.CurrentCultureIgnoreCase)
                .Select(title => $"- {title.Trim()}"));
    }

    private static string BuildRotationTaskListText(IReadOnlyList<RotationNotificationTaskMailItem> tasks)
    {
        if (tasks.Count == 0)
        {
            return "- Keine offenen Aufgaben.";
        }

        return string.Join(
            "\n",
            tasks.Select(task =>
            {
                var dueLabel = task.DueDate.HasValue
                    ? $" (fällig: {task.DueDate.Value:dd.MM.yyyy})"
                    : string.Empty;
                return $"- {task.Title}{dueLabel}";
            }));
    }

    private static string NormalizeRotationPlaceholder(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "n. a." : value.Trim();
    }

    private static string BuildWorkflowDisplayName(WorkflowListItemDto workflow)
    {
        return $"{workflow.FirstName} {workflow.LastName}".Trim();
    }

    private static string BuildWorkflowDisplayName(WorkflowDetailDto workflow)
    {
        return $"{workflow.FirstName} {workflow.LastName}".Trim();
    }

    private static string BuildWorkflowAccessUrl(
        string frontendBaseUrl,
        Guid workflowUid,
        NotificationDispatchBatch batch)
    {
        var normalizedBaseUrl = frontendBaseUrl.TrimEnd('/');
        var target = batch.PrimaryTarget;
        var redirectPath = string.Equals(batch.NotificationType, NotificationTemplateKeys.TaskReady, StringComparison.OrdinalIgnoreCase)
            ? "/tasks/my"
            : target.PreferredPath switch
            {
                "/supervisor" => "/supervisor",
                "/tasks/my" => "/tasks/my",
                "/workflows" => $"/workflows/{workflowUid}",
                _ => $"/workflows/{workflowUid}"
            };

        return $"{normalizedBaseUrl}{redirectPath}";
    }

    private static string BuildRotationAccessUrl(string frontendBaseUrl, string linkPath)
    {
        var normalizedBaseUrl = frontendBaseUrl.TrimEnd('/');
        var normalizedPath = string.IsNullOrWhiteSpace(linkPath)
            ? "/tasks/my"
            : linkPath.Trim();

        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        return $"{normalizedBaseUrl}{normalizedPath}";
    }

    private static IReadOnlyList<NotificationDispatchBatch> GroupWorkflowTargets(
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets)
    {
        return targets
            .GroupBy(target =>
                string.Join(
                    "\u001f",
                    target.NotificationType.Trim(),
                    target.TargetEmail.Trim(),
                    target.RecipientUserId?.ToString() ?? string.Empty,
                    target.PreferredPath ?? string.Empty),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedTargets = group
                    .OrderBy(target => target.WorkflowTaskId ?? long.MaxValue)
                    .ThenBy(target => target.NotificationId)
                    .ToList();

                return new NotificationDispatchBatch
                {
                    NotificationType = orderedTargets[0].NotificationType,
                    PrimaryTarget = orderedTargets[0],
                    TaskTitles = orderedTargets
                        .Select(target => target.TaskTitle)
                        .Where(taskTitle => !string.IsNullOrWhiteSpace(taskTitle))
                        .Select(taskTitle => taskTitle!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(taskTitle => taskTitle, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                };
            })
            .ToList();
    }

    private static string GetWorkflowBlockingReason(string templateKey)
    {
        return templateKey switch
        {
            NotificationTemplateKeys.WorkflowCreated => "Für diesen Vorgang sind aktuell keine Empfänger für den Startfall auflösbar.",
            NotificationTemplateKeys.TaskReady => "Für diesen Vorgang gibt es aktuell keine benachrichtigbaren offenen Aufgaben.",
            NotificationTemplateKeys.WorkflowCompleted => "Für diesen Vorgang ist aktuell keine Abschlussbenachrichtigung triggerbar.",
            _ => "Für diesen Vorgang ist aktuell keine Vorschau verfügbar."
        };
    }

    private static string GetRotationBlockingReason(string templateKey)
    {
        return templateKey switch
        {
            NotificationTemplateKeys.UpcomingChange => "Für diesen Durchlaufplan ist aktuell kein bevorstehender Wechsel benachrichtigbar.",
            NotificationTemplateKeys.Reminder => "Für diesen Durchlaufplan gibt es aktuell keine heute fälligen benachrichtigbaren Aufgaben.",
            NotificationTemplateKeys.Overdue => "Für diesen Durchlaufplan gibt es aktuell keine benachrichtigbaren überfälligen Aufgaben.",
            _ => "Für diesen Durchlaufplan ist aktuell keine Vorschau verfügbar."
        };
    }

    private sealed class NotificationDispatchBatch
    {
        public required string NotificationType { get; init; }
        public required WorkflowNotificationDispatchTarget PrimaryTarget { get; init; }
        public required List<string> TaskTitles { get; init; }
    }
}
