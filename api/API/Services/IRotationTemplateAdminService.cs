namespace API;

internal interface IRotationTemplateAdminService
{
    Task<IReadOnlyList<DepartmentActionTemplateDto>> GetDepartmentActionTemplatesAsync(
        int? departmentId,
        bool? isActive,
        CancellationToken cancellationToken = default);
    Task<AdminListPageDto<DepartmentActionTemplateDto>> GetDepartmentActionTemplatesAsync(
        int? departmentId,
        bool? isActive,
        AdminListQuery query,
        CancellationToken cancellationToken = default);

    Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplateAsync(
        int templateId,
        CancellationToken cancellationToken = default);

    Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplateAsync(
        DepartmentActionTemplateUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplateAsync(
        int templateId,
        DepartmentActionTemplateUpsertRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDepartmentActionTemplateAsync(
        int templateId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
}
