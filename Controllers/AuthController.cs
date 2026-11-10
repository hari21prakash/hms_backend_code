using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalManagement.Api.Authentication;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IOptions<JwtOptions> jwtOptions, IHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "hms.refresh";
    private readonly JwtOptions _jwt = jwtOptions.Value;

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.RegisterAsync(request, GetIpAddress(), cancellationToken);
        SetRefreshCookie(session);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(session.Response, "Account created."));
    }

    [AllowAnonymous]
    [HttpPost("patient-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PatientLogin(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginPatientAsync(request, GetIpAddress(), cancellationToken);
        SetRefreshCookie(session);
        return Ok(ApiResponse<AuthResponseDto>.Ok(session.Response, "Signed in."));
    }

    [AllowAnonymous]
    [HttpPost("staff-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StaffLogin(StaffLoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginStaffAsync(request, GetIpAddress(), cancellationToken);
        SetRefreshCookie(session);
        return Ok(ApiResponse<AuthResponseDto>.Ok(session.Response, "Signed in."));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(ApiResponse.Failure("A refresh token is required."));

        try
        {
            var session = await authService.RefreshAsync(refreshToken, GetIpAddress(), cancellationToken);
            SetRefreshCookie(session);
            return Ok(ApiResponse<AuthResponseDto>.Ok(session.Response, "Session refreshed."));
        }
        catch (UnauthorizedAppException)
        {
            ClearRefreshCookie();
            return Unauthorized(ApiResponse.Failure("Refresh token is invalid or expired."));
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        ClearRefreshCookie();
        return Ok(ApiResponse.Ok("Signed out."));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<AuthUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAppException();

        var user = await authService.GetUserAsync(userId, cancellationToken);
        return Ok(ApiResponse<AuthUserDto>.Ok(user));
    }

    private void SetRefreshCookie(AuthSession session)
    {
        Response.Cookies.Append(RefreshCookieName, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = environment.IsProduction() || Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true
        });
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = environment.IsProduction() || Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth"
    });

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}