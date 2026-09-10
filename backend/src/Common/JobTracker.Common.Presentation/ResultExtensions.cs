using JobTracker.Common.Domain;
using Microsoft.AspNetCore.Http;

namespace JobTracker.Common.Presentation;

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
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
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
