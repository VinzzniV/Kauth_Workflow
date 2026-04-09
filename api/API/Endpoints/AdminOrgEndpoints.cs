using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminOrgEndpoints
{
    public static IEndpointRouteBuilder MapAdminOrgEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/auth/users", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminUsers());
        }).Produces<List<AdminUserDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/roles", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminRoles());
        }).Produces<List<AdminRoleDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/groups", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminGroups());
        }).Produces<List<AdminGroupDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/permissions", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminPermissions());
        }).Produces<List<AdminPermissionDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/audit", async (
            [FromQuery] int? limit,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminPermissionAudit(limit ?? 100));
        }).Produces<List<AdminPermissionAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/master-data/departments", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminDepartmentAssignments());
        }).Produces<List<AdminDepartmentAssignmentDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/master-data/departments", async (
            [FromBody] AdminDepartmentCreateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var department = await userAuthorizationRepository.CreateDepartment(request.DepartmentName);
                return Results.Ok(department);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDepartmentAssignmentDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/master-data/departments/{departmentId:int}", async (
            int departmentId,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var deleted = await userAuthorizationRepository.DeleteDepartment(departmentId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Department not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/master-data/responsibilities", async (
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await userAuthorizationRepository.GetAdminResponsibilityOwners());
        }).Produces<List<AdminResponsibilityOwnerDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/master-data/responsibilities", async (
            [FromBody] AdminResponsibilityCreateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var responsibility = await userAuthorizationRepository.CreateResponsibility(
                    request.ResponsibilityName,
                    request.DepartmentId);
                return Results.Ok(responsibility);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminResponsibilityOwnerDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/master-data/responsibilities/{responsibilityId:int}", async (
            int responsibilityId,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var deleted = await userAuthorizationRepository.DeleteResponsibility(responsibilityId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Responsibility not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/master-data/users", async (
            [FromBody] AdminUserCreateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var user = await userAuthorizationRepository.CreateUser(
                    request.ExternalKey,
                    request.DisplayName,
                    request.Email,
                    request.NotificationEmail,
                    request.DepartmentId,
                    request.IsActive,
                    access.User?.UserId);
                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/master-data/users/{userId:long}", async (
            long userId,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var deleted = await userAuthorizationRepository.DeleteUser(userId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "User not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/master-data/users/{userId:long}", async (
            long userId,
            [FromBody] AdminUserMasterDataUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var user = await userAuthorizationRepository.UpdateUserMasterData(
                    userId,
                    request.ExternalKey,
                    request.DisplayName,
                    request.Email,
                    request.NotificationEmail,
                    request.DepartmentId,
                    request.IsActive,
                    access.User?.UserId);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/master-data/departments/{departmentId:int}", async (
            int departmentId,
            [FromBody] AdminDepartmentAssignmentUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var assignment = await userAuthorizationRepository.UpdateDepartmentAssignment(
                    departmentId,
                    request.DepartmentLeadUserId,
                    request.RequirementOwnerUserId);
                if (assignment is null)
                {
                    return Results.NotFound(new { message = "Department not found." });
                }

                return Results.Ok(assignment);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDepartmentAssignmentDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/master-data/responsibilities/{responsibilityId:int}", async (
            int responsibilityId,
            [FromBody] AdminResponsibilityOwnerUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var assignment = await userAuthorizationRepository.UpdateResponsibilityOwner(
                    responsibilityId,
                    request.AppUserId,
                    request.DepartmentId);
                if (assignment is null)
                {
                    return Results.NotFound(new { message = "Responsibility not found." });
                }

                return Results.Ok(assignment);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminResponsibilityOwnerDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/users/{userId:long}/roles", async (
            long userId,
            [FromBody] AdminUserRoleUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var user = await userAuthorizationRepository.UpdateUserRoles(userId, request.RoleIds);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/users/{userId:long}/groups", async (
            long userId,
            [FromBody] AdminUserGroupUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var user = await userAuthorizationRepository.UpdateUserGroups(userId, request.GroupIds);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/groups/{groupId:int}/roles", async (
            int groupId,
            [FromBody] AdminGroupRoleUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var group = await userAuthorizationRepository.UpdateGroupRoles(groupId, request.RoleIds);
                if (group is null)
                {
                    return Results.NotFound(new { message = "Group not found." });
                }

                return Results.Ok(group);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminGroupDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/roles/{roleId:int}/permissions", async (
            int roleId,
            [FromBody] AdminRolePermissionUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var role = await userAuthorizationRepository.UpdateRolePermissions(
                    roleId,
                    request.PermissionIds,
                    access.User?.UserId);
                if (role is null)
                {
                    return Results.NotFound(new { message = "Role not found." });
                }

                return Results.Ok(role);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminRoleDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/users/{userId:long}/permission-overrides", async (
            long userId,
            [FromBody] AdminUserPermissionOverrideUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var user = await userAuthorizationRepository.UpdateUserPermissionOverrides(
                    userId,
                    request.Overrides,
                    access.User?.UserId);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
