using FluentValidation;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

/// <summary>
/// Shape only. Whether a date is in the past is BR-1 and belongs to the
/// aggregate: a validator that duplicated it would be a second place to change
/// when the rule changes, and the two would drift.
/// </summary>
internal sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Street).NotEmpty();
        RuleFor(command => command.City).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
        RuleFor(command => command.ZipCode).NotEmpty();
        RuleFor(command => command.AssigneeId).NotEmpty();
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}
