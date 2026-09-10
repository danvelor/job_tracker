using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class AssigneeConfiguration : IEntityTypeConfiguration<Assignee>
{
    public void Configure(EntityTypeBuilder<Assignee> builder)
    {
        builder.ToTable("assignees");
        builder.HasKey(assignee => assignee.Id);

        builder.Property(assignee => assignee.OrganizationId).IsRequired();
        builder.Property(assignee => assignee.Name).IsRequired().HasMaxLength(200);

        builder.HasData(
            new { Id = RosterSeed.AssigneeOrtiz, OrganizationId = RosterSeed.DevelopmentOrganization, Name = "J. Ortiz" },
            new { Id = RosterSeed.AssigneeRuiz, OrganizationId = RosterSeed.DevelopmentOrganization, Name = "M. Ruiz" },
            new { Id = RosterSeed.AssigneeOther, OrganizationId = RosterSeed.SecondOrganization, Name = "K. Lawson" });
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.OrganizationId).IsRequired();
        builder.Property(customer => customer.Name).IsRequired().HasMaxLength(200);

        builder.Property(customer => customer.Email).IsRequired().HasMaxLength(320);

        builder.HasData(
            new { Id = RosterSeed.CustomerAcme, OrganizationId = RosterSeed.DevelopmentOrganization, Name = "Acme Holdings", Email = "ops@acme.test" },
            new { Id = RosterSeed.CustomerBirch, OrganizationId = RosterSeed.DevelopmentOrganization, Name = "Birch Property", Email = "facilities@birch.test" },
            new { Id = RosterSeed.CustomerOther, OrganizationId = RosterSeed.SecondOrganization, Name = "Cedar Estates", Email = "admin@cedar.test" });
    }
}
