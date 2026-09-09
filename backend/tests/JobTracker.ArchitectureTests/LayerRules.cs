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

    [Fact]
    public void Presentation_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsPresentation);

        // An endpoint that can reach a DbContext is an endpoint that
        // eventually does, and the layering becomes a diagram rather than a
        // constraint. The composition root is the only project allowed to see
        // both, and it is not this one.
        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Presentation_cannot_even_see_infrastructure()
    {
        // The rule above inspects IL, so it catches a layer that *uses* the one
        // below. This one reads the project file, so it catches a layer that
        // merely *can* — the state a mistake starts in, and the one nothing
        // else notices. Neither the compiled assembly's reference list nor
        // NetArchTest can see an unused reference: the compiler omits it from
        // the manifest entirely.
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
        // Stated as a whitelist rather than a blacklist: a new dependency on
        // the innermost layer has to be argued for here, rather than slipped
        // past a list of things somebody once thought to forbid.
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.Domain")
            .Should().BeEquivalentTo(["JobTracker.Common.Domain"]);
    }

    /// <summary>
    /// The project's declared references, read from the csproj. Everything else
    /// available to a test — the loaded assembly, its manifest, NetArchTest —
    /// describes what the code uses; only this describes what it is allowed to.
    /// </summary>
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

        // The rule that keeps the model portable: a domain that knows about
        // HTTP cannot be driven by a background job, a console tool or a test
        // without one.
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
        // Worth more than several narrower rules: a project with no references
        // cannot leak a domain type, an EF attribute or a MediatR marker into
        // the contract Billing compiles against.
        ProjectReferencesOf("Modules/Jobs/JobTracker.Modules.Jobs.IntegrationEvents")
            .Should().BeEmpty();
    }

    [Fact]
    public void The_contract_carries_primitives_only()
    {
        // The rule above stops a reference; this stops a type from this
        // assembly leaking into a contract member — a nested record would
        // compile and would still be a shape consumers must version with us.
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
}
