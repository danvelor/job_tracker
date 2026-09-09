using FluentAssertions;
using JobTracker.Common.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Common.Presentation.UnitTests;

/// <summary>
/// The whole error contract, and it is pure — no host, no request, no
/// database. Every endpoint in the Jobs module inherits these guarantees
/// without a test of its own for them.
/// </summary>
public sealed class ResultExtensionsTests
{
    private static ProblemDetails ProblemFrom(Result result) =>
        result.ToProblem().Should().BeOfType<ProblemHttpResult>().Subject.ProblemDetails;

    [Fact]
    public void A_validation_error_becomes_400()
    {
        ProblemFrom(Result.Failure(Error.Validation("job.title", "Title is required")))
            .Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void A_not_found_error_becomes_404()
    {
        ProblemFrom(Result.Failure(Error.NotFound("job.not-found", "No such job")))
            .Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void An_invariant_refusal_becomes_409_rather_than_400()
    {
        // Design B6, and the distinction that matters. 400 tells the client to
        // fix its input; the input was fine and the job had already been
        // completed.
        ProblemFrom(Result.Failure(Error.Conflict("job.terminal", "Job is closed")))
            .Status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void An_unauthorized_error_becomes_401()
    {
        ProblemFrom(Result.Failure(new Error("auth", "No", ErrorType.Unauthorized)))
            .Status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void An_unclassified_failure_becomes_500_rather_than_400()
    {
        // The mirror image of the rule above. Defaulting the other way reports
        // a server fault as the caller's mistake, and every such bug gets
        // closed as a client error.
        ProblemFrom(Result.Failure(new Error("x", "y", ErrorType.Failure)))
            .Status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void The_error_code_and_message_travel_in_the_body()
    {
        var problem = ProblemFrom(Result.Failure(Error.NotFound("job.not-found", "No such job")));

        // The frontend switches on kind and quotes the code in a bug report. A
        // status alone cannot tell two 409s apart.
        problem.Extensions["errorCode"].Should().Be("job.not-found");
        problem.Detail.Should().Be("No such job");
    }

    [Fact]
    public void Field_errors_travel_under_errors()
    {
        var fields = new Dictionary<string, string[]> { ["Title"] = ["Title is required"] };

        var problem = ProblemFrom(
            Result.Failure(Error.Validation("job.validation", "Validation failed", fields)));

        problem.Extensions.Should().ContainKey("errors");
        problem.Extensions["errors"].Should().BeSameAs(fields);
    }

    [Fact]
    public void An_error_with_no_field_errors_omits_the_key_rather_than_sending_an_empty_object()
    {
        var problem = ProblemFrom(Result.Failure(Error.NotFound("job.not-found", "No such job")));

        // A client that tests for the key's presence should not have to also
        // test whether it is empty.
        problem.Extensions.Should().NotContainKey("errors");
    }

    [Fact]
    public void Asking_a_success_for_a_problem_is_a_defect_rather_than_a_500()
    {
        var read = () => Result.Success().ToProblem();

        // Returning something plausible would hide a caller that forgot to
        // check IsSuccess, and the endpoint would answer 500 to a request that
        // worked.
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_returns_the_success_shape_when_the_result_succeeded()
    {
        Result.Success(42).Match(Results.Ok).Should().BeOfType<Ok<int>>();
    }

    [Fact]
    public void Match_returns_the_problem_when_the_result_failed()
    {
        Result.Failure<int>(Error.NotFound("a", "b")).Match(Results.Ok)
            .Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public void Match_on_a_valueless_result_still_distinguishes_the_two()
    {
        // The transition endpoints return 204 and have no value to map, so
        // they use this overload — and it must not silently succeed.
        Result.Success().Match(Results.NoContent).Should().BeOfType<NoContent>();
        Result.Failure(Error.Conflict("a", "b")).Match(Results.NoContent)
            .Should().BeOfType<ProblemHttpResult>();
    }
}
