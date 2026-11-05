using Core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations
{
    public class SystemTaskConfiguration : IEntityTypeConfiguration<SystemTask>
    {
        public void Configure(EntityTypeBuilder<SystemTask> builder)
        {
            builder.ToTable("SystemTasks");
            builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Route).HasMaxLength(200);
            builder.HasIndex(x => x.Code).IsUnique();
        }
    }

    public class AccessLevelConfiguration : IEntityTypeConfiguration<AccessLevel>
    {
        public void Configure(EntityTypeBuilder<AccessLevel> builder)
        {
            builder.ToTable("AccessLevels");
            builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.HasIndex(x => x.Name);
        }
    }

    public class AccessLevelTaskConfiguration : IEntityTypeConfiguration<AccessLevelTask>
    {
        public void Configure(EntityTypeBuilder<AccessLevelTask> builder)
        {
            builder.ToTable("AccessLevelTasks");
            builder.HasKey(x => new { x.AccessLevelId, x.SystemTaskId });

            builder.HasOne(x => x.AccessLevel)
                .WithMany(a => a.AccessLevelTasks)
                .HasForeignKey(x => x.AccessLevelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.SystemTask)
                .WithMany(t => t.AccessLevelTasks)
                .HasForeignKey(x => x.SystemTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class UserAccessLevelConfiguration : IEntityTypeConfiguration<Core.Entities.User>
    {
        public void Configure(EntityTypeBuilder<Core.Entities.User> builder)
        {
            builder.Property(u => u.UserType).IsRequired(false);

            builder.HasOne(u => u.AccessLevel)
                .WithMany()
                .HasForeignKey(u => u.AccessLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
