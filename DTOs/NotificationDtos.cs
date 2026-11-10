using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt,
    string? RelatedEntityName,
    Guid? RelatedEntityId);

public record NotificationPageDto(
    IReadOnlyList<NotificationDto> Notifications,
    int TotalCount,
    int Page,
    int PageSize,
    int UnreadCount);

/// <summary>Result of "mark all as read": how many were changed and what is still unread (always 0 afterwards).</summary>
public record NotificationReadAllResultDto(int MarkedCount, int UnreadCount);

public record AuditLogDto(
    Guid Id,
    DateTime Timestamp,
    string Action,
    string EntityName,
    string? EntityId,
    string Description,
    string? UserEmail,
    string? UserRole,
    string? IpAddress);

public record AuditLogPageDto(
    IReadOnlyList<AuditLogDto> Logs,
    int TotalCount,
    int Page,
    int PageSize);
