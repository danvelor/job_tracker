using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", table =>
        {
            // BR-4 and BR-5 at the level of the data. The aggregate enforces
            // both, but a row written by anything other than the aggregate — a
            // migration, a psql session, a future service — must not be able to
            // contradict them.
            table.HasCheckConstraint(
                "ck_jobs_completed_has_signature",
                "status <> 'Completed' OR signature_url IS NOT NULL");

            table.HasCheckConstraint(
                "ck_jobs_cancelled_has_reason",
                "status <> 'Cancelled' OR cancellation_reason IS NOT NULL");

            // Storing the status as text buys readability; without this it
            // would also buy the freedom to store nonsense, which the ordinal
            // it replaced at least did not allow.
            //
            // Built from the enum rather than from a literal list, so there is
            // one source of truth. Adding a JobStatus without generating a
            // migration still fails: the test database carries the constraint
            // this migration wrote, not the one the enum now describes.
            table.HasCheckConstraint("ck_jobs_status", StatusIsOneOfTheDefinedValues());
        });

        builder.HasKey(job => job.Id);

        // The list of domain events is not state; the outbox in plan 4 carries
        // it. Without this EF sees a collection of an interface and fails to
        // build the model.
        builder.Ignore(job => job.DomainEvents);

        builder.Property(job => job.Title).IsRequired().HasMaxLength(200);
        builder.Property(job => job.Description);
        builder.Property(job => job.CancellationReason);
        builder.Property(job => job.SignatureUrl);

        // Text, not an ordinal: readable in a psql session, diff-friendly in a
        // migration, and safe against the reordering that makes an
        // integer-backed enum dangerous across deployments.
        builder.Property(job => job.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(job => job.OrganizationId).IsRequired();
        builder.Property(job => job.CustomerId).IsRequired();

        // No identity of its own, so no table of its own. Flattened onto the
        // job row (architecture 6.2).
        builder.OwnsOne(job => job.Address, address =>
        {
            address.Property(value => value.Street).HasColumnName("street").IsRequired();
            address.Property(value => value.City).HasColumnName("city").IsRequired();
            address.Property(value => value.State).HasColumnName("state").IsRequired();
            address.Property(value => value.ZipCode).HasColumnName("zip_code").IsRequired();
            address.Property(value => value.Latitude)
                .HasColumnName("latitude").HasPrecision(9, 6).IsRequired();
            address.Property(value => value.Longitude)
                .HasColumnName("longitude").HasPrecision(9, 6).IsRequired();
        });
        builder.Navigation(job => job.Address).IsRequired();

        builder.HasMany(job => job.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Through the field, not through the property: Photos returns a fresh
        // read-only wrapper on every call, which EF cannot add to.
        builder.Metadata
            .FindNavigation(nameof(Job.Photos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // NFR-6. Shadow properties: when a job was created and last touched is
        // an audit concern, and no business rule reads either, so neither
        // belongs on the aggregate.
        builder.Property<DateTimeOffset>("CreatedAt")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property<DateTimeOffset>("UpdatedAt")
            .HasDefaultValueSql("now()");

        // D-26. The rosters are read-only, but the reference is real: a job
        // must not point at an assignee that does not exist.
        builder.HasOne<Assignee>()
            .WithMany()
            .HasForeignKey(job => job.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(job => job.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string StatusIsOneOfTheDefinedValues()
    {
        var values = string.Join(", ", Enum.GetNames<JobStatus>().Select(name => $"'{name}'"));
        return $"status in ({values})";
    }
}
