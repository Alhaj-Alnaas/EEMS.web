using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations
{
    public class LaborCompanyConfiguration : IEntityTypeConfiguration<LaborCompany>
    {
        public void Configure(EntityTypeBuilder<LaborCompany> builder)
        {
            builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.Specialty).HasMaxLength(200);
            builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
            builder.Property(x => x.PermitCode).HasMaxLength(2).IsRequired();
            builder.HasIndex(x => x.Number).IsUnique().HasFilter("[isDeleted] = 0");
            builder.HasIndex(x => x.PermitCode).IsUnique().HasFilter("[isDeleted] = 0");
        }
    }

    public class LaborWorkerConfiguration : IEntityTypeConfiguration<LaborWorker>
    {
        public void Configure(EntityTypeBuilder<LaborWorker> builder)
        {
            builder.Property(x => x.PermitNo).HasMaxLength(16).IsRequired();
            builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Nationality).HasMaxLength(80);
            builder.Property(x => x.PassportNo).HasMaxLength(40);
            builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.Notes).HasMaxLength(500);
            builder.HasIndex(x => x.PermitNo).IsUnique().HasFilter("[isDeleted] = 0");
            builder.HasIndex(x => x.CompanyId);
            builder.HasOne(x => x.Company)
                .WithMany(x => x.Workers)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class LaborImportBatchConfiguration : IEntityTypeConfiguration<LaborImportBatch>
    {
        public void Configure(EntityTypeBuilder<LaborImportBatch> builder)
        {
            builder.Property(x => x.Mode).HasMaxLength(16).IsRequired();
            builder.Property(x => x.State).HasMaxLength(16).IsRequired();
            builder.Property(x => x.FileName).HasMaxLength(260);
            builder.HasIndex(x => x.CompanyId);
            builder.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class LaborImportLineConfiguration : IEntityTypeConfiguration<LaborImportLine>
    {
        public void Configure(EntityTypeBuilder<LaborImportLine> builder)
        {
            builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
            builder.Property(x => x.CurrentPermitNo).HasMaxLength(16);
            builder.Property(x => x.ProposedPermitNo).HasMaxLength(16);
            builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Nationality).HasMaxLength(80);
            builder.Property(x => x.PassportNo).HasMaxLength(40);
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.Notes).HasMaxLength(500);
            builder.Property(x => x.CurrentStatus).HasMaxLength(20);
            builder.Property(x => x.ProposedStatus).HasMaxLength(20);
            builder.HasIndex(x => x.BatchId);
            builder.HasOne(x => x.Batch)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
