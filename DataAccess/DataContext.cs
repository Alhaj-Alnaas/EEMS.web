using Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // مهم جدًا للـ Identity
            modelBuilder.Entity<Gate>()
                .HasMany(g => g.PermitTypes)
                .WithMany(p => p.Gates)
                .UsingEntity(j => j.ToTable("GatePermitTypes"));
        }
        public DbSet<GateUsedFor> GatesUsedFor { get; set; }
        public DbSet<User> SystemUser { get; set; }
        public DbSet<UserRoles> UsersRoles { get; set; }
       
    }
}
