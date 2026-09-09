using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal sealed class JobPhotoConfiguration : IEntityTypeConfiguration<JobPhoto>
{
    public void Configure(EntityTypeBuilder<JobPhoto> builder)
    {
        builder.ToTable("job_photos");
        builder.HasKey(photo => photo.Id);

        // The aggregate assigns the identity, not the store. Without this EF
        // treats the key as store-generated, reads a non-default value as
        // evidence the row already exists, and emits UPDATE instead of INSERT
        // for a photo attached to an already-persisted job.
        builder.Property(photo => photo.Id).ValueGeneratedNever();

        builder.Property(photo => photo.Url).IsRequired().HasMaxLength(2048);
        builder.Property(photo => photo.CapturedAt).IsRequired();
        builder.Property(photo => photo.Caption).HasMaxLength(500);

        // No query filter: a photo is reachable only through its job, and the
        // job is filtered. Adding one here would need a join on every read to
        // enforce something the parent already guarantees.
    }
}
