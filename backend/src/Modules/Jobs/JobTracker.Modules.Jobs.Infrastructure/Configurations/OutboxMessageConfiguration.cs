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

        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type).IsRequired().HasMaxLength(500);
        builder.Property(message => message.Content).IsRequired().HasColumnType("jsonb");
        builder.Property(message => message.OccurredOn).IsRequired();
    }
}
