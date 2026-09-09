using FluentAssertions;
using FluentValidation;
using JobTracker.Common.Application.Behaviors;
using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class ValidationBehaviorTests
{
    private sealed record Probe(string Name) : IRequest<Result>;

    private sealed record ValuedProbe(string Name) : IRequest<Result<Guid>>;

    private sealed class ProbeValidator : AbstractValidator<Probe>
    {
        public ProbeValidator() => RuleFor(probe => probe.Name).NotEmpty();
    }

    private sealed class ValuedProbeValidator : AbstractValidator<ValuedProbe>
    {
        public ValuedProbeValidator() => RuleFor(probe => probe.Name).NotEmpty();
    }

    [Fact]
    public async Task A_valid_request_reaches_the_handler()
    {
        var reached = false;
        var behavior = new ValidationBehavior<Probe, Result>([new ProbeValidator()]);

        await behavior.Handle(
            new Probe("ok"),
            () => { reached = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task An_invalid_request_never_reaches_the_handler()
    {
        var reached = false;
        var behavior = new ValidationBehavior<Probe, Result>([new ProbeValidator()]);

        var result = await behavior.Handle(
            new Probe(""),
            () => { reached = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        reached.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task With_no_validator_registered_the_request_passes_through()
    {
        var behavior = new ValidationBehavior<Probe, Result>([]);

        var result = await behavior.Handle(
            new Probe(""),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_failing_generic_request_returns_a_well_formed_failure()
    {
        // The behaviour builds a failure for a Result<T> response, and the
        // plan's self-review flagged that the plain Result it constructs
        // cannot be cast to Result<Guid>. This is the test that proves the
        // construction is type-correct rather than throwing at the cast.
        var behavior = new ValidationBehavior<ValuedProbe, Result<Guid>>([new ValuedProbeValidator()]);

        var result = await behavior.Handle(
            new ValuedProbe(""),
            () => Task.FromResult(Result.Success(Guid.NewGuid())),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Every_broken_rule_is_reported_rather_than_only_the_first()
    {
        var behavior = new ValidationBehavior<Probe, Result>(
            [new ProbeValidator(), new SecondRule()]);

        var result = await behavior.Handle(
            new Probe(""),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        result.Error.Message.Should().Contain(";");
    }

    private sealed class SecondRule : AbstractValidator<Probe>
    {
        public SecondRule() =>
            RuleFor(probe => probe.Name).MinimumLength(3).WithMessage("too short");
    }
}
