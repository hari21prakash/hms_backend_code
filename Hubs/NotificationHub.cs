using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalManagement.Api.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HospitalManagement.Api.Hubs;

/// <summary>
/// Server-to-client channel for notifications. Clients only RECEIVE; there are no client-callable methods, so a user
/// cannot send, subscribe to or read anyone else's notifications. Requires a valid JWT and the same
/// <c>dashboard.read</c> permission as the REST endpoints; connections are addressed by the token's user id.
/// </summary>
[HasPermission("dashboard.read")]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
}

/// <summary>Maps a SignalR connection to the user id in the JWT "sub" claim (inbound claim mapping is disabled in this app).</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? connection.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
