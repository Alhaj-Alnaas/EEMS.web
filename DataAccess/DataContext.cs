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

        // ---------------- Legacy Phase 2 sets (unchanged) ----------------
        public DbSet<Permit> Permits { get; set; }
        public DbSet<CarMovment> CarsMovment { get; set; }
        public DbSet<EquipMatiMovment> EquipsMatisMovment { get; set; }
        public DbSet<ProcedureMovment> ProceduresMovment { get; set; }
        public DbSet<Gate> Gates { get; set; }
        public DbSet<PermitType> PermitTypes { get; set; }

        public DbSet<DepartmentDto> DepartmentsDto { get; set; }

        // ---------------- Phase 1: Access Control ----------------
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
        public DbSet<Reader> Readers { get; set; }
        public DbSet<ReaderDowntime> ReaderDowntimes { get; set; }
        public DbSet<EmployeePermission> EmployeePermissions { get; set; }
        public DbSet<DeviceCommand> DeviceCommands { get; set; }
        public DbSet<MovementLog> MovementLogs { get; set; }
        public DbSet<ReaderDeviceStats> ReaderDeviceStats { get; set; }

        // ---------------- Phase 2: Workflow ----------------
        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
        public DbSet<WorkflowStep> WorkflowSteps { get; set; }
        public DbSet<PermitApproval> PermitApprovals { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // ---------------- Phase 2: Unified permit details ----------------
        public DbSet<EquipmentPermitDetail> EquipmentPermitDetails { get; set; }
        public DbSet<VisitorPermitDetail> VisitorPermitDetails { get; set; }
        public DbSet<VehiclePermitDetail> VehiclePermitDetails { get; set; }


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

            modelBuilder.Entity<ProcedureMovment>()
                .HasOne(pr => pr.permit)
                .WithMany(p => p.Procedures)
                .HasForeignKey(pr => pr.permitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DepartmentDto>().HasNoKey().ToView(null);

            // Orphan entities not (yet) wired into the unified model — excluded so EF
            // does not try to map them or create tables for them.
            modelBuilder.Ignore<ApproveMovment>();
            modelBuilder.Ignore<Role>();
            modelBuilder.Ignore<Permission>();
            modelBuilder.Ignore<UserRole>();
            modelBuilder.Ignore<RolePermission>();

            // ================= Unified Permit Management additions =================

            // ---- Phase 1: Access Control ----
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BiometricTemplate>()
                .HasOne(b => b.Employee)
                .WithMany(e => e.BiometricTemplates)
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BiometricTemplate>()
                .Property(b => b.TemplateType)
                .HasConversion<string>();

            modelBuilder.Entity<Reader>()
                .HasOne(r => r.Gate)
                .WithMany(g => g.Readers)
                .HasForeignKey(r => r.GateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Reader>()
                .Property(r => r.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Reader>()
                .HasIndex(r => r.DeviceSerial)
                .IsUnique()
                .HasFilter("[isDeleted] = 0");

            modelBuilder.Entity<Reader>()
                .Property(r => r.DeviceSerial)
                .HasMaxLength(50);

            modelBuilder.Entity<Reader>()
                .Property(r => r.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<Reader>()
                .Property(r => r.IpAddress)
                .HasMaxLength(45);

            modelBuilder.Entity<Reader>()
                .Property(r => r.Model)
                .HasMaxLength(50);

            modelBuilder.Entity<EmployeePermission>()
                .HasOne(ep => ep.Employee)
                .WithMany(e => e.Permissions)
                .HasForeignKey(ep => ep.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeePermission>()
                .HasOne(ep => ep.Gate)
                .WithMany()
                .HasForeignKey(ep => ep.GateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeviceCommand>()
                .HasOne(dc => dc.Reader)
                .WithMany()
                .HasForeignKey(dc => dc.ReaderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeviceCommand>()
                .Property(dc => dc.Status)
                .HasConversion<string>();

            modelBuilder.Entity<ReaderDeviceStats>()
                .HasOne(s => s.Reader)
                .WithMany()
                .HasForeignKey(s => s.ReaderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReaderDeviceStats>()
                .Property(s => s.DeviceSerial)
                .HasMaxLength(50);

            modelBuilder.Entity<ReaderDeviceStats>()
                .HasIndex(s => new { s.ReaderId, s.isDeleted })
                .HasFilter("[isDeleted] = 0");

            modelBuilder.Entity<MovementLog>()
                .HasOne(m => m.Employee)
                .WithMany(e => e.MovementLogs)
                .HasForeignKey(m => m.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MovementLog>()
                .HasOne(m => m.Permit)
                .WithMany(p => p.MovementLogs)
                .HasForeignKey(m => m.PermitId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MovementLog>()
                .HasOne(m => m.Gate)
                .WithMany()
                .HasForeignKey(m => m.GateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovementLog>()
                .HasOne(m => m.Reader)
                .WithMany()
                .HasForeignKey(m => m.ReaderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MovementLog>()
                .Property(m => m.Direction)
                .HasConversion<string>();

            modelBuilder.Entity<MovementLog>()
                .Property(m => m.Source)
                .HasConversion<string>();

            // NOTE: SQL Server doesn't allow non-deterministic CHECK constraints, but a simple
            // "EmployeeId IS NOT NULL OR PermitId IS NOT NULL" constraint is deterministic and valid.
            modelBuilder.Entity<MovementLog>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_MovementLog_EmployeeOrPermit",
                    "[EmployeeId] IS NOT NULL OR [PermitId] IS NOT NULL OR ([DevicePin] IS NOT NULL AND [DevicePin] <> '')"));

            modelBuilder.Entity<MovementLog>()
                .Property(m => m.DevicePin)
                .HasMaxLength(50);

            // ---- Phase 2: Workflow ----
            modelBuilder.Entity<WorkflowDefinition>()
                .Property(w => w.PermitClassification)
                .HasConversion<string>();

            modelBuilder.Entity<WorkflowStep>()
                .HasOne(s => s.WorkflowDefinition)
                .WithMany(w => w.Steps)
                .HasForeignKey(s => s.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkflowStep>()
                .Property(s => s.StepType)
                .HasConversion<string>();

            modelBuilder.Entity<PermitApproval>()
                .HasOne(a => a.Permit)
                .WithMany(p => p.Approvals)
                .HasForeignKey(a => a.PermitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PermitApproval>()
                .HasOne(a => a.WorkflowStep)
                .WithMany()
                .HasForeignKey(a => a.WorkflowStepId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PermitApproval>()
                .Property(a => a.Decision)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.RelatedPermit)
                .WithMany()
                .HasForeignKey(n => n.RelatedPermitId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.RelatedReader)
                .WithMany()
                .HasForeignKey(n => n.RelatedReaderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ReaderDowntime>()
                .HasOne(d => d.Reader)
                .WithMany()
                .HasForeignKey(d => d.ReaderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReaderDowntime>()
                .Property(d => d.StatusDuringOutage)
                .HasConversion<string>();

            modelBuilder.Entity<ReaderDowntime>()
                .HasIndex(d => new { d.ReaderId, d.EndedAt });

            modelBuilder.Entity<Permit>()
                .HasOne(p => p.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(p => p.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);

            // SQL Server rejects multiple cascade/set-null paths from Employees → Permits.
            modelBuilder.Entity<Permit>()
                .HasOne(p => p.RequesterEmployee)
                .WithMany()
                .HasForeignKey(p => p.RequesterEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Permit>()
                .HasOne(p => p.HostEmployee)
                .WithMany()
                .HasForeignKey(p => p.HostEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Permit>()
                .HasOne(p => p.Gate)
                .WithMany()
                .HasForeignKey(p => p.GateId)
                .OnDelete(DeleteBehavior.NoAction);

            // ---- Phase 2: Unified permit details (1:1 with Permit) ----
            modelBuilder.Entity<EquipmentPermitDetail>()
                .HasOne(d => d.Permit)
                .WithOne(p => p.EquipmentDetail)
                .HasForeignKey<EquipmentPermitDetail>(d => d.PermitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VisitorPermitDetail>()
                .HasOne(d => d.Permit)
                .WithOne(p => p.VisitorDetail)
                .HasForeignKey<VisitorPermitDetail>(d => d.PermitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VisitorPermitDetail>()
                .HasOne(d => d.HostEmployee)
                .WithMany()
                .HasForeignKey(d => d.HostEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<VehiclePermitDetail>()
                .HasOne(d => d.Permit)
                .WithOne(p => p.VehicleDetail)
                .HasForeignKey<VehiclePermitDetail>(d => d.PermitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
