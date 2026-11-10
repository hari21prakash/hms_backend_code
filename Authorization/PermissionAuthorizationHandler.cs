using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace HospitalManagement.Api.Authorization;

public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    public const string ClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(ClaimType, requirement.Code))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}