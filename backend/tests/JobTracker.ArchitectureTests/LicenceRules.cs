using FluentAssertions;
using MediatR;

namespace JobTracker.ArchitectureTests;

/// <summary>
/// D-20. Nothing fails today if MediatR moves to 13 or FluentAssertions to 8:
/// it compiles, the tests pass, and the defect appears when a reviewer runs
/// dotnet restore without a licence and cannot build the deliverable.
///
/// Asserting on the loaded assembly rather than on Directory.Packages.props
/// checks what was actually restored, and catches a transitive bump the file
/// would not mention.
/// </summary>
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
        // D-20. Hangfire's LICENSE.md is multi-licensed with LGPL v3 among the
        // options, which referencing the unmodified package satisfies. The paid
        // tier is the separate Hangfire.Pro.* packages — and a reviewer running
        // dotnet restore without a licence is otherwise who finds out.
        typeof(Hangfire.BackgroundJob).Assembly.GetName().Name.Should().Be("Hangfire.Core");

        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name!)
            .Should().NotContain(name => name.StartsWith("Hangfire.Pro", StringComparison.Ordinal));
    }
}
