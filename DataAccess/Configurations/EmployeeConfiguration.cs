using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.Property(e => e.FileNumber).HasMaxLength(50).IsRequired();
            builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            builder.Property(e => e.JobTitle).HasMaxLength(200);
            builder.Property(e => e.Address).HasMaxLength(500);
            builder.Property(e => e.Phone).HasMaxLength(50);
            builder.Property(e => e.Email).HasMaxLength(200);
            builder.Property(e => e.PhotoPath).HasMaxLength(400);
            builder.Property(e => e.CardNumber).HasMaxLength(50);
            builder.Property(e => e.DepartmentName).HasMaxLength(200);
            builder.Property(e => e.BloodType).HasMaxLength(20);
            builder.Property(e => e.Nationality).HasMaxLength(100);
            // Soft-deleted / blank PIN stubs may collide; keep uniqueness on real pins only.
            builder.HasIndex(e => e.FileNumber)
                .IsUnique()
                .HasFilter("[isDeleted] = 0 AND [FileNumber] IS NOT NULL AND [FileNumber] <> ''");
        }
    }

    public class EmployeeCardConfiguration : IEntityTypeConfiguration<EmployeeCard>
    {
        public void Configure(EntityTypeBuilder<EmployeeCard> builder)
        {
            builder.Property(c => c.CardNumber).HasMaxLength(50).IsRequired();
            builder.Property(c => c.PhotoPath).HasMaxLength(400);
            builder.Property(c => c.PrintedBy).HasMaxLength(100);
            builder.Property(c => c.Status).HasMaxLength(30).IsRequired();

            builder.HasOne(c => c.Employee)
                .WithMany(e => e.Cards)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
