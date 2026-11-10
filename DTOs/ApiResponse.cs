namespace HospitalManagement.Api.DTOs;

/// <summary>Consistent envelope for every API response (success or failure).</summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string? TraceId { get; init; }

    public static ApiResponse Failure(string message, IEnumerable<string>? errors = null, string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = (errors ?? Array.Empty<string>()).ToList(),
            TraceId = traceId
        };

    public static ApiResponse Ok(string message = "Success") =>
        new() { Success = true, Message = message };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, T? data = default, IEnumerable<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Data = data,
            Errors = (errors ?? Array.Empty<string>()).ToList()
        };
}
