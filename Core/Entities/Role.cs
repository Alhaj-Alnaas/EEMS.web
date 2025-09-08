using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }        // اسم الدور (Admin, ApplicationUser, إلخ)
        public string DisplayName { get; set; }  // اسم العرض
        public string Description { get; set; } // وصف الدور

        public List<RolePermission> RolePermissions { get; set; } = new();
        public List<UserRole> UserRoles { get; set; } = new();
    }
}

