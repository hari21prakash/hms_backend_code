using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalManagement.Api.Exceptions;

namespace HospitalManagement.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's id from the JWT. Use this for every "acting user" value
    /// (created by, received by, recorded by, performed by) - never accept it from a request body.
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAppException();

        return userId;
    }
}
