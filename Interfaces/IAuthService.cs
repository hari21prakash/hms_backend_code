using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IAuthService
{
    Task<AuthSession> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);
    /// <summary>Patient portal login: only accounts with the Patient role are accepted.</summary>
    Task<AuthSession> LoginPatientAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    /// <summary>Staff portal login: the selected role must match the account's role, and Patient accounts are rejected.</summary>
    Task<AuthSession> LoginStaffAsync(StaffLoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<AuthUserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}