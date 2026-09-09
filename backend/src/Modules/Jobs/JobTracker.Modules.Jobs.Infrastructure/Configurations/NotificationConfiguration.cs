using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", table =>
            // A notification that claims to have been sent without recording
            // when is a record that cannot be audited, which defeats the point
            // of keeping one.
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

        // The whole of this consumer's idempotency (4.5). Two handlers write
        // into this table for two different recipients, which is why one
        // constraint serves both.
        builder.HasIndex(
                notification => new { notification.SourceEventId, notification.Recipient })
            .IsUnique()
            .HasDatabaseName("uq_notifications_idempotency");

        builder.HasIndex(notification => notification.CreatedAt)
            .HasDatabaseName("ix_notifications_pending")
            .HasFilter("status = 'Pending'");
    }
}
