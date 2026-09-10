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
            table.HasTrigger("tr_jobs_touch_updated_at");

            table.HasCheckConstraint(
                "ck_jobs_completed_has_signature",
                "status <> 'Completed' OR signature_url IS NOT NULL");

            table.HasCheckConstraint(
                "ck_jobs_cancelled_has_reason",
                "status <> 'Cancelled' OR cancellation_reason IS NOT NULL");

            table.HasCheckConstraint("ck_jobs_status", StatusIsOneOfTheDefinedValues());
        });

        builder.HasKey(job => job.Id);

        builder.Ignore(job => job.DomainEvents);

        builder.Property(job => job.Title).IsRequired().HasMaxLength(200);
        builder.Property(job => job.Description);
        builder.Property(job => job.CancellationReason);
        builder.Property(job => job.SignatureUrl);

        builder.Property(job => job.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(job => job.OrganizationId).IsRequired();
        builder.Property(job => job.CustomerId).IsRequired();

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

        builder.Metadata
            .FindNavigation(nameof(Job.Photos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<DateTimeOffset>("CreatedAt")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property<DateTimeOffset>("UpdatedAt")
            .HasDefaultValueSql("now()");

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
