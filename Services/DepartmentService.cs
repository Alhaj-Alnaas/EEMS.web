using Core.Entities.DTOs;
using Core.Interfaces.Services;
using Core.Interfaces.UnitOfWork;
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
            if (string.IsNullOrWhiteSpace(responsibilityCode))
                return new List<DepartmentDto>();

            try
            {
                const string storedProcedure = "EXEC sp_show_Req_dep {0}";
                return await _unitOfWork.StoredProcedures
                    .ExecuteStoredProcedureAsync<DepartmentDto>(storedProcedure, responsibilityCode);
            }
            catch (Exception ex) when (IsMissingDepartmentProcedure(ex))
            {
                // The legacy procedure is not deployed in every database.
                // Callers fall back to the signed-in user's own department.
                return new List<DepartmentDto>();
            }
        }

        private static bool IsMissingDepartmentProcedure(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message ?? "";
                if (message.Contains("sp_show_Req_dep", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("stored procedure", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("الإجراء المخزن", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public async Task<DepartmentDto?> GetSingleDepartmentByResponsibilityAsync(string responsibilityCode)
        {
            var departments = await GetDepartmentsByResponsibilityAsync(responsibilityCode);
            return departments.FirstOrDefault(d => d.RespCode == responsibilityCode);
        }

    }
}
