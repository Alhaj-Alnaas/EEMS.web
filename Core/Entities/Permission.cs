using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Permission
    {
        public int Id { get; set; }
        public string Name { get; set; }        // اسم الصلاحية (مثال: ViewGates)
        public string DisplayName { get; set; }  // اسم العرض (مثال: عرض البوابات)
        public string Description { get; set; } // وصف الصلاحية
        public string Category { get; set; }    // فئة الصلاحية (البوابات، التصاريح، المستخدمين)

        public List<RolePermission> RolePermissions { get; set; } = new();
    }
}

