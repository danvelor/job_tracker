using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", table =>
            table.HasCheckConstraint(
                "ck_notifications_sent_has_timestamp",
                "status <> 'Sent' OR sent_at IS NOT NULL"));

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.SourceEventId).IsRequired();
        builder.Property(notification => notification.OrganizationId).IsRequired();
        builder.Property(notification => notification.Recipient).IsRequired().HasMaxLength(320);
        builder.Property(notification => notification.Subject).IsRequired().HasMaxLength(200);
        builder.Property(notification => notification.Body).IsRequired();
        builder.Property(notification => notification.Status)
            .HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(notification => notification.CreatedAt).IsRequired();

        builder.HasIndex(
                notification => new { notification.SourceEventId, notification.Recipient })
            .IsUnique()
            .HasDatabaseName("uq_notifications_idempotency");

        builder.HasIndex(notification => notification.CreatedAt)
            .HasDatabaseName("ix_notifications_pending")
            .HasFilter("status = 'Pending'");
    }
}
