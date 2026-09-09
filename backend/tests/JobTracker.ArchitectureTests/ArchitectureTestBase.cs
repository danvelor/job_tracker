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

    /// <summary>
    /// Asserts a rule holds AND that it examined something.
    ///
    /// This is the permanent half of architecture 8.3's two mechanisms. A
    /// NetArchTest assertion over an empty set passes, so a renamed suffix or
    /// a moved namespace turns a rule into a rule about nothing — silently.
    /// Requiring a non-empty subject means a rule can never quietly stop
    /// applying.
    /// </summary>
    protected static void ShouldHold(ConditionList condition, PredicateList subject) =>
        Assert(condition, subject.GetTypes());

    /// <summary>
    /// The same guard for a rule with no <c>That()</c> filter. Types.InAssembly
    /// returns <see cref="Types"/>, and only <c>That()</c> narrows it to a
    /// <see cref="PredicateList"/> — a layer rule examines the whole assembly,
    /// so it never calls one.
    /// </summary>
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
