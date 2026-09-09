using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);

        // The key is the event's identity, assigned by the domain (D-34), so
        // the store must not think it generates one.
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type).IsRequired().HasMaxLength(500);
        builder.Property(message => message.Content).IsRequired().HasColumnType("jsonb");
        builder.Property(message => message.OccurredOn).IsRequired();

        // No tenant filter and no organization column. An outbox row is
        // machinery, not business data: the drain must see every tenant's
        // messages, and the events inside carry their own organization.
    }
}
