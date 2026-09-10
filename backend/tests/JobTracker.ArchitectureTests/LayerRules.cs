using System.Xml.Linq;
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

        offenders.Should().BeEmpty(
            "state changes go through intention-named methods (architecture 9.2)");
    }

    [Fact]
    public void Presentation_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsPresentation);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Presentation_cannot_even_see_infrastructure()
    {
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.Presentation")
            .Should().NotContain("JobTracker.Modules.Jobs.Infrastructure");
    }

    [Fact]
    public void Application_cannot_even_see_infrastructure()
    {
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.Application")
            .Should().NotContain("JobTracker.Modules.Jobs.Infrastructure");
    }

    [Fact]
    public void Domain_sees_only_the_shared_kernel()
    {
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.Domain")
            .Should().BeEquivalentTo(["JobTracker.Common.Domain"]);
    }

    private static IReadOnlyList<string> ProjectReferencesOf(string projectPath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the solution they describe");

        var file = Path.Combine(directory!.FullName, "src", projectPath,
            $"{Path.GetFileName(projectPath)}.csproj");

        File.Exists(file).Should().BeTrue("{0} must exist for this rule to mean anything", file);

        return XDocument.Load(file)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();
    }

    [Fact]
    public void Presentation_does_not_reference_entity_framework()
    {
        var subject = Types.InAssembly(JobsPresentation);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore"),
            subject);
    }

    [Fact]
    public void Domain_does_not_reference_ASP_NET()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("Microsoft.AspNetCore"), subject);
    }

    [Fact]
    public void Application_does_not_reference_ASP_NET()
    {
        var subject = Types.InAssembly(JobsApplication);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("Microsoft.AspNetCore"), subject);
    }

    [Fact]
    public void The_contract_project_can_see_nothing_at_all()
    {
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.IntegrationEvents")
            .Should().BeEmpty();
    }

    [Fact]
    public void The_contract_carries_primitives_only()
    {
        var members = Types.InAssembly(JobsIntegrationEvents)
            .That().AreClasses().GetTypes()
            .SelectMany(type => type.GetProperties())
            .Select(property => property.PropertyType)
            .Where(type => !type.IsPrimitive
                           && type != typeof(string)
                           && type != typeof(Guid)
                           && type != typeof(decimal)
                           && type != typeof(DateTimeOffset)
                           && type != typeof(DateOnly)
                           && type != typeof(Type))
            .Select(type => type.Name)
            .ToList();

        members.Should().BeEmpty();
    }

    [Fact]
    public void Billing_cannot_see_the_Jobs_domain()
    {
        var references = ProjectReferencesOf("Modules/Billing/JobTracker.Modules.Billing.Application");

        references.Should().Contain("JobTracker.Modules.Jobs.IntegrationEvents");
        references.Should().NotContain("JobTracker.Modules.Jobs.Domain");
        references.Should().NotContain("JobTracker.Modules.Jobs.Application");
        references.Should().NotContain("JobTracker.Modules.Jobs.Infrastructure");
    }

    [Fact]
    public void Billings_domain_sees_only_the_shared_kernel()
    {
        ProjectReferencesOf("Modules/Billing/JobTracker.Modules.Billing.Domain")
            .Should().BeEquivalentTo(["JobTracker.Common.Domain"]);
    }

    [Fact]
    public void Billing_does_not_use_a_Jobs_type_even_transitively()
    {
        var subject = Types.InAssembly(BillingApplication);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Domain"), subject);
    }
}
