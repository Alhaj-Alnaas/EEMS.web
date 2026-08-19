using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations
{
    /// <summary>
    /// Sample <see cref="IEntityTypeConfiguration{TEntity}"/> demonstrating the
    /// preferred pattern for new entity configuration going forward, instead of
    /// growing <see cref="DataContext.OnModelCreating"/> further. Apply new
    /// configurations with modelBuilder.ApplyConfigurationsFromAssembly(...).
    /// </summary>
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.Property(e => e.FileNumber).HasMaxLength(50).IsRequired();
            builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            builder.HasIndex(e => e.FileNumber).IsUnique();
        }
    }
}
