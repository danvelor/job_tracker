using FluentAssertions;
using JobTracker.Common.Domain;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.ArchitectureTests;

public sealed class TenantRules
{
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

        var undeclared = context.Model.GetEntityTypes()
            .Where(entity => entity.FindProperty("OrganizationId") is not null)
            .Where(entity => !typeof(ITenantScoped).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.ClrType.Name)
            .ToList();

        undeclared.Should().BeEmpty();
    }
}
