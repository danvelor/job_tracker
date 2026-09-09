using FluentAssertions;
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public sealed class NamingRules : ArchitectureTestBase
{
    [Fact]
    public void Commands_are_public_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication).That().HaveNameEndingWith("Command");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Command_handlers_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication).That().HaveNameEndingWith("CommandHandler");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Queries_are_public_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication).That().HaveNameEndingWith("Query");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Query_handlers_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication).That().HaveNameEndingWith("QueryHandler");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Validators_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication).That().HaveNameEndingWith("Validator");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Domain_events_are_public_sealed_and_live_in_the_domain()
    {
        var subject = Types.InAssembly(JobsDomain).That().HaveNameEndingWith("DomainEvent");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Repository_interfaces_live_in_the_domain()
    {
        var subject = Types.InAssembly(JobsDomain)
            .That().AreInterfaces().And().HaveNameEndingWith("Repository");

        ShouldHold(subject.Should().BePublic(), subject);
    }

    [Fact]
    public void No_repository_interface_leaked_into_the_application_layer()
    {
        Types.InAssembly(JobsApplication)
            .That().AreInterfaces().And().HaveNameEndingWith("Repository")
            .GetTypes().Should().BeEmpty(
                "a repository interface belongs to the domain that owns the aggregate");
    }
}
