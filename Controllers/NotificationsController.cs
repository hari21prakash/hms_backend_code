using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [HasPermission("dashboard.read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] NotificationType? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var userId = GetCurrentUserId();
        var result = await notificationService.GetNotificationsAsync(userId, page, pageSize, unreadOnly, type, cancellationToken);
        return Ok(ApiResponse<NotificationPageDto>.Ok(result));
    }

    [HttpPost("{id:guid}/read")]
    [HasPermission("dashboard.read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await notificationService.MarkNotificationAsReadAsync(id, userId, cancellationToken);
        return Ok(ApiResponse<NotificationDto>.Ok(result, "Notification marked as read."));
    }

    [HttpPost("read-all")]
    [HasPermission("dashboard.read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationReadAllResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkAllAsReadAsync(GetCurrentUserId(), cancellationToken);
        return Ok(ApiResponse<NotificationReadAllResultDto>.Ok(result, "All notifications marked as read."));
    }

    [HttpGet("unread-count")]
    [HasPermission("dashboard.read")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var count = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count));
    }

    private Guid GetCurrentUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException("The current user is not identified.");

        return userId;
    }
}
