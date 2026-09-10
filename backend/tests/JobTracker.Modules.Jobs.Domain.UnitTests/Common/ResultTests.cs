using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void A_success_carries_its_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void A_failure_carries_its_error()
    {
        var error = Error.Conflict("job.terminal", "A terminal job cannot change state");

        var result = Result.Failure<int>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws_because_that_is_a_defect()
    {
        var result = Result.Failure<int>(Error.NotFound("job.missing", "No such job"));

        var read = () => result.Value;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_success_cannot_be_constructed_with_an_error()
    {
        var build = () => new TestableResult(true, Error.Validation("x", "y"));

        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_failure_cannot_be_constructed_without_an_error()
    {
        var build = () => new TestableResult(false, Error.None);

        build.Should().Throw<InvalidOperationException>();
    }

    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
