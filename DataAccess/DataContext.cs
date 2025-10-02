using Core.Entities;
using Core.Entities.DTOs;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;


namespace DataAccess
{
    public class DataContext : IdentityDbContext<User>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }
        public DbSet<Permit> Permits { get; set; }
        public DbSet<CarMovment> CarsMovment { get; set; }
        public DbSet<EquipMatiMovment> EquipsMatisMovment { get; set; }
        public DbSet<HumanMovment> HumansMovment { get; set; }
        public DbSet<ProcedureMovment> ProceduresMovment { get; set; }
        public DbSet<Gate> Gates { get; set; }
        public DbSet<PermitType> PermitTypes { get; set; }

        public DbSet<DepartmentDto> DepartmentsDto { get; set; }

       
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); 
            modelBuilder.Entity<Gate>()
                .HasMany(g => g.PermitTypes)
                .WithMany(p => p.Gates)
                .UsingEntity(j => j.ToTable("GatePermitTypes"));

            modelBuilder.Entity<Permit>()
        .Property(p => p.type)
        .HasConversion<string>();

            // permit linked

            modelBuilder.Entity<EquipMatiMovment>()
        .HasOne(e => e.permit)
        .WithMany(p => p.EquipmentsAndMatirials)
        .HasForeignKey(e => e.permitId)
        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarMovment>()
                .HasOne(c => c.permit)
                .WithMany(p => p.Cars)
                .HasForeignKey(c => c.permitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HumanMovment>()
                .HasOne(h => h.Permit)
                .WithMany(p => p.Humans)
                .HasForeignKey(h => h.PermitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProcedureMovment>()
                .HasOne(pr => pr.permit)
                .WithMany(p => p.Procedures)
                .HasForeignKey(pr => pr.permitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DepartmentDto>().HasNoKey();
        }
    }
}
