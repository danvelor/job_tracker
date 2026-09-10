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

        builder.Property(photo => photo.Id).ValueGeneratedNever();

        builder.Property(photo => photo.Url).IsRequired().HasMaxLength(2048);
        builder.Property(photo => photo.CapturedAt).IsRequired();
        builder.Property(photo => photo.Caption).HasMaxLength(500);
    }
}
