using Core.Entities.DTOs;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DepartmentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<DepartmentDto>> GetDepartmentsByResponsibilityAsync(string responsibilityCode)
        {
            string storedProcedure = "EXEC sp_show_Req_dep {0}";
            return await _unitOfWork.StoredProcedures.ExecuteStoredProcedureAsync<DepartmentDto>(storedProcedure, responsibilityCode);
        }

        public async Task<DepartmentDto?> GetSingleDepartmentByResponsibilityAsync(string responsibilityCode)
        {
            var departments = await GetDepartmentsByResponsibilityAsync(responsibilityCode);
            return departments.FirstOrDefault(d => d.RespCode == responsibilityCode);
        }

    }
}
