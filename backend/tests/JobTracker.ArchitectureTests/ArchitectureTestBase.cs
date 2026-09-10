using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public abstract class ArchitectureTestBase
{
    protected static readonly Assembly JobsDomain =
        typeof(Modules.Jobs.Domain.Job).Assembly;

    protected static readonly Assembly JobsApplication =
        typeof(Modules.Jobs.Application.Jobs.CreateJob.CreateJobCommand).Assembly;

    protected static readonly Assembly JobsPresentation =
        Modules.Jobs.Presentation.JobsPresentation.Assembly;

    protected static readonly Assembly JobsIntegrationEvents =
        typeof(Modules.Jobs.IntegrationEvents.JobCompletedIntegrationEvent).Assembly;

    protected static readonly Assembly BillingApplication =
        typeof(Modules.Billing.Application.BillingOptions).Assembly;

    protected static readonly Assembly CommonDomain = typeof(Common.Domain.Entity).Assembly;

    protected static void ShouldHold(ConditionList condition, PredicateList subject) =>
        Assert(condition, subject.GetTypes());

    protected static void ShouldHold(ConditionList condition, Types subject) =>
        Assert(condition, subject.GetTypes());

    private static void Assert(ConditionList condition, IEnumerable<Type> subject)
    {
        subject.Should().NotBeEmpty(
            "an architecture rule over an empty set passes without checking anything");

        var result = condition.GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these types break the rule: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
