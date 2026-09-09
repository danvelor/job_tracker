using FluentAssertions;
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public sealed class LayerRules : ArchitectureTestBase
{
    [Fact]
    public void Domain_does_not_reference_application()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Application"),
            subject);
    }

    [Fact]
    public void Domain_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsApplication);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Domain_does_not_reference_entity_framework()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore"), subject);
    }

    [Fact]
    public void Common_domain_knows_nothing_about_jobs()
    {
        // The rule that keeps a shared kernel a kernel: if a type would only
        // ever be used by one module, it lives in that module (architecture 3.3).
        var subject = Types.InAssembly(CommonDomain);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("JobTracker.Modules"), subject);
    }

    [Fact]
    public void Aggregates_expose_no_public_setter()
    {
        var offenders = Types.InAssembly(JobsDomain)
            .That().Inherit(typeof(Common.Domain.AggregateRoot))
            .GetTypes()
            .SelectMany(type => type.GetProperties())
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        // NetArchTest cannot express this, and it is the rule that separates a
        // domain model from a data bag, so it is written by hand rather than
        // left unchecked.
        offenders.Should().BeEmpty(
            "state changes go through intention-named methods (architecture 9.2)");
    }
}
