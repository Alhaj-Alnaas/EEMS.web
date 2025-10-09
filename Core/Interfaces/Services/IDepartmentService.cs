using Core.Entities.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IDepartmentService
    {
        public  Task<List<DepartmentDto>> GetDepartmentsByResponsibilityAsync(string responsibilityCode);

        public  Task<DepartmentDto?> GetSingleDepartmentByResponsibilityAsync(string responsibilityCode);
    }
}
