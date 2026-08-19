using System;
using System.Collections.Generic;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1 organizational department master used by employee access control.
    /// Distinct from the legacy <see cref="Core.Entities.DTOs.DepartmentDto"/> which is
    /// sourced from the external stored procedure (sp_show_Req_dep).
    /// </summary>
    public class Department : Base
    {
        public string Code { get; set; } = "";
        public string NameAr { get; set; } = "";
        public string NameEn { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
