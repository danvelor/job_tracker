using FluentValidation;
using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Common.Application.Behaviors;

/// <summary>
/// The Open/Closed example: validation applies to every handler in every
/// module and not one handler mentions it. Adding an authorisation behaviour
/// is a registration, not an edit.
///
/// It returns a failed <see cref="Result"/> rather than throwing, because a
/// validation failure is expected (architecture 9.2).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = validators
            .Select(validator => validator.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        // Every broken rule, not the first: telling someone one problem at a
        // time hides the way forward (design A5 point 3 makes the same case
        // for the create form). That applies within a field as much as across
        // them, so the grouping keeps every message rather than the last.
        var fieldErrors = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        var error = Error.Validation(
            "request.validation",
            string.Join("; ", failures.Select(failure => failure.ErrorMessage)),
            fieldErrors);

        return CreateFailure(error);
    }

    /// <summary>
    /// A plain <c>Result.Failure(error)</c> is not a <c>Result&lt;T&gt;</c>, so
    /// casting one would throw for any handler returning a value. Reflecting
    /// the generic factory builds the right shape for both, and the constraint
    /// guarantees there is no third case.
    /// </summary>
    private static TResponse CreateFailure(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];

        var failure = typeof(Result)
            .GetMethods()
            .Single(method =>
                method is { Name: nameof(Result.Failure), IsGenericMethod: true })
            .MakeGenericMethod(valueType)
            .Invoke(null, [error])!;

        return (TResponse)failure;
    }
}
