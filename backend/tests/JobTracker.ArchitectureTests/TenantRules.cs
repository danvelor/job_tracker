using FluentAssertions;
using JobTracker.Common.Domain;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.ArchitectureTests;

/// <summary>
/// Layer 4 of architecture 7.2. Layers 1-3 stop a mistake from being exploited;
/// this one stops the mistake from being made — adding a tenant-scoped table
/// and forgetting its filter fails the build rather than leaking in production.
/// </summary>
public sealed class TenantRules
{
    /// <summary>
    /// Building the model needs no database, only a provider that knows how to
    /// translate. Nothing here opens a connection.
    /// </summary>
    private static JobsDbContext ModelOnlyContext() =>
        new(
            new DbContextOptionsBuilder<JobsDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only")
                .UseSnakeCaseNamingConvention()
                .Options,
            new MutableTenantContext(Guid.Empty));

    [Fact]
    public void Every_tenant_scoped_entity_has_a_query_filter()
    {
        using var context = ModelOnlyContext();

        var scoped = context.Model.GetEntityTypes()
            .Where(entity => typeof(ITenantScoped).IsAssignableFrom(entity.ClrType))
            .ToList();

        scoped.Should().NotBeEmpty(
            "an architecture rule over an empty set passes without checking anything");

        var unfiltered = scoped
            .Where(entity => entity.GetQueryFilter() is null)
            .Select(entity => entity.ClrType.Name)
            .ToList();

        unfiltered.Should().BeEmpty();
    }

    [Fact]
    public void Every_entity_carrying_an_organization_declares_itself_tenant_scoped()
    {
        using var context = ModelOnlyContext();

        // The rule above only sees entities that remembered to implement the
        // interface. This one catches the earlier mistake: a table with an
        // organization column that never declared itself, and so was never
        // asked for a filter.
        var undeclared = context.Model.GetEntityTypes()
            .Where(entity => entity.FindProperty("OrganizationId") is not null)
            .Where(entity => !typeof(ITenantScoped).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.ClrType.Name)
            .ToList();

        undeclared.Should().BeEmpty();
    }
}
