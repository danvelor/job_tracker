using JobTracker.Common.Domain;
using Microsoft.AspNetCore.Http;

namespace JobTracker.Common.Presentation;

/// <summary>
/// The one place a <see cref="Result"/> becomes an HTTP response. Endpoints
/// call <see cref="Match{T}"/> and never build a status code themselves, so the
/// mapping cannot drift between routes and adding an <see cref="ErrorType"/> is
/// a change in one file.
/// </summary>
public static class ResultExtensions
{
    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.ToProblem();

    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.ToProblem();

    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            // A caller that reaches here forgot to check IsSuccess. Answering
            // 500 to a request that worked would hide the bug in a production
            // log; throwing puts it in front of the test that caused it.
            throw new InvalidOperationException("A successful result has no problem to report.");
        }

        var error = result.Error;

        return Results.Problem(
            title: TitleFor(error.Type),
            detail: error.Message,
            statusCode: StatusFor(error.Type),
            extensions: Extensions(error));
    }

    private static Dictionary<string, object?> Extensions(Error error)
    {
        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };

        // Absent rather than empty: a client that tests for the key should not
        // also have to test whether it holds anything.
        if (error.FieldErrors is not null)
        {
            extensions["errors"] = error.FieldErrors;
        }

        return extensions;
    }

    private static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        // The state refused a well-formed request (design B6).
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        // Anything unclassified is ours, not the caller's. Defaulting to 400
        // would report a server fault as a client mistake.
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "The job's state refused this operation",
        ErrorType.Unauthorized => "Unauthorized",
        _ => "An unexpected error occurred",
    };
}
