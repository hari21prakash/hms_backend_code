using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Interfaces;

public interface INotificationService
{
    Task<NotificationPageDto> GetNotificationsAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken);
    Task<NotificationPageDto> GetNotificationsAsync(Guid userId, int page, int pageSize, bool unreadOnly, NotificationType? type, CancellationToken cancellationToken);
    Task<NotificationReadAllResultDto> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationDto> MarkNotificationAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuditLogPageDto> GetAuditLogsAsync(string? search, string? entityName, int page, int pageSize, CancellationToken cancellationToken);
}
