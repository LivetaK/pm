namespace pm.Application.Exceptions;

public class ApiConflictException : Exception
{
    public ApiConflictException(string message) : base(message)
    {
    }
}
