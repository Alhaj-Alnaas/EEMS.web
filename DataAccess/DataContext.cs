using Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public DbSet<GateUsedFor> GatesUsedFor { get; set; }
        public DbSet<User> SystemUser { get; set; }
        public DbSet<UserRoles> UsersRoles { get; set; }
       
    }
}
