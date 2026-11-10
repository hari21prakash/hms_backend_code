namespace HospitalManagement.Api.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, message) { }

    public NotFoundException(string entity, object key)
        : base(StatusCodes.Status404NotFound, $"{entity} with identifier '{key}' was not found.") { }
}

public class RequestValidationException : AppException
{
    public RequestValidationException(string message, IEnumerable<string>? errors = null)
        : base(StatusCodes.Status400BadRequest, message, errors) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, message) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(StatusCodes.Status403Forbidden, message) { }
}

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Authentication is required.")
        : base(StatusCodes.Status401Unauthorized, message) { }
}
