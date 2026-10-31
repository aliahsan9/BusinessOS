namespace BusinessOS.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : AppException
{
    public string? ErrorCode { get; }

    public ConflictException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message)
    {
    }
}
