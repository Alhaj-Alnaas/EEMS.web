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

        //public async Task<object> GetDepartmentsByResponsibilityAsync(string responsibilityCode, bool single = false)
        //{
        //    string storedProcedure = "EXEC sp_show_Req_dep {0}";

        //    var departments = await _unitOfWork.StoredProcedures
        //        .ExecuteStoredProcedureAsync<DepartmentDto>(storedProcedure, responsibilityCode);

        //    if (single)
        //    {
        //        // نرجع إدارة واحدة فقط (أول نتيجة)
        //        return departments.FirstOrDefault(d => d.RespCode == responsibilityCode);
        //    }

        //    // نرجع كل الإدارات (افتراضي)
        //    return departments;
        //}

        //Task<List<DepartmentDto>> GetDepartmentsByResponsibilityAsync(string responsibilityCode, bool single)
        //{
        //    throw new NotImplementedException();
        //}

        //public async Task<List<DepartmentDto>> GetDepartmentsByResponsibilityAsync(string responsibilityCode)
        //{
        //    // لاحظ أننا نستخدم الـ Repository الخاص بالـ SP من خلال الـ UnitOfWork
        //    string storedProcedure = "EXEC sp_show_Req_dep {0}";
        //    var departments = await _unitOfWork.StoredProcedures
        //        .ExecuteStoredProcedureAsync<DepartmentDto>(storedProcedure, responsibilityCode);

        //    return departments;
        //}
    }
}
