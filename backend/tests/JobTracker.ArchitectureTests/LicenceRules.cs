using FluentAssertions;
using MediatR;

namespace JobTracker.ArchitectureTests;

public sealed class LicenceRules
{
    [Fact]
    public void MediatR_stays_below_the_commercially_licensed_major()
    {
        typeof(IMediator).Assembly.GetName().Version!.Major.Should().Be(12);
    }

    [Fact]
    public void FluentAssertions_stays_below_the_commercially_licensed_major()
    {
        typeof(FluentAssertions.AssertionExtensions).Assembly.GetName().Version!.Major
            .Should().BeLessThan(8);
    }

    [Fact]
    public void Hangfire_is_the_open_source_package_rather_than_Pro()
    {
        typeof(Hangfire.BackgroundJob).Assembly.GetName().Name.Should().Be("Hangfire.Core");

        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name!)
            .Should().NotContain(name => name.StartsWith("Hangfire.Pro", StringComparison.Ordinal));
    }
}
