namespace HospitalManagement.Api.Exceptions;

/// <summary>Base type for expected, user-facing errors. Mapped to HTTP responses by the exception middleware.</summary>
public class AppException : Exception
{
    public AppException(int statusCode, string message, IEnumerable<string>? errors = null) : base(message)
    {
        StatusCode = statusCode;
        Errors = (errors ?? Array.Empty<string>()).ToList();
    }

    public int StatusCode { get; }
    public IReadOnlyList<string> Errors { get; }
}
