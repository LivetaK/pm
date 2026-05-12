namespace pm.Application.Exceptions;

public class ApiValidationException : Exception
{
    public ApiValidationException(string message) : base(message)
    {
        Errors = [message];
    }

    public ApiValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
