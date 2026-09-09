namespace JobTracker.Common.Domain;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Failure,
}

/// <summary>
/// <paramref name="FieldErrors"/> rides on the error rather than on a channel
/// of its own, because ValidationBehavior produces it inside the Application
/// layer and a <see cref="Result"/> is the only thing that layer can return.
/// Presentation lifts it into the <c>errors</c> member of a ProblemDetails.
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error Validation(
        string code, string message, IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(code, message, ErrorType.Validation, fieldErrors);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    /// <summary>
    /// An invariant refused the operation. Presentation maps this to 409, not
    /// 400: the request was well-formed and the state refused it (design B6).
    /// </summary>
    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);
}
