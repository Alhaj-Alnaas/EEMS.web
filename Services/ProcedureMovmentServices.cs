using System;
using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public class ProcedureMovmentServices : IProcedureMovment
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProcedureMovmentServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task DeleteProcedureMovment(ProcedureMovment procedureMovment)
        {
            _unitOfWork.ProcedureMovment.Delete(procedureMovment);
            await _unitOfWork.SaveAsync();
        }

        public List<string> GetProcedureMovment(ProcedureMovment procedureMovment)
        {
            throw new NotImplementedException();
        }

        public  async Task InsertProcedureMovment(ProcedureMovment procedureMovment)
        {
                
        {
            if (procedureMovment == null) throw new ArgumentNullException(nameof(procedureMovment));

            _unitOfWork.ProcedureMovment.Insert(procedureMovment);
            await _unitOfWork.SaveAsync();
        }
        }


        public async Task<List<string>> SearchByProcedureMovment(ProcedureMovment procedureMovment)
        {
            // جلب جميع الإجراءات من قاعدة البيانات
            var allProcedures = await _unitOfWork.ProcedureMovment.GetAllAsync();

            // تطبيق شروط البحث باستخدام LINQ
            var query = allProcedures
                .Where(p =>
                    (string.IsNullOrEmpty(procedureMovment.donedBy) || p.donedBy.Contains(procedureMovment.donedBy)) &&
                    (string.IsNullOrEmpty(procedureMovment.doneAs) || p.doneAs.Contains(procedureMovment.doneAs)) &&
                    (string.IsNullOrEmpty(procedureMovment.shift) || p.shift.Contains(procedureMovment.shift)) &&
                    (procedureMovment.procedureType == '\0' || p.procedureType == procedureMovment.procedureType)
                )
                .Select(p => $"تم بواسطة: {p.donedBy} - التاريخ: {p.doneOn:yyyy-MM-dd} - النوع: {p.procedureType}")
                .ToList();

            return query;
        }

        public async Task UpdateProcedureMovment(ProcedureMovment procedureMovment)
        {
            // 1. جلب الحركة الأصلية من قاعدة البيانات
            var existingProcedureMovment = await _unitOfWork.ProcedureMovment.GetByIdAsync(procedureMovment.Id);
            if (existingProcedureMovment == null)
                throw new Exception("حركة الإجراء غير موجودة");

            // 2. تحديث الحقول الأساسية
            existingProcedureMovment.permitId = procedureMovment.permitId;
            existingProcedureMovment.donedBy = procedureMovment.donedBy;
            existingProcedureMovment.doneOn = procedureMovment.doneOn;
            existingProcedureMovment.doneAs = procedureMovment.doneAs;
            existingProcedureMovment.shift = procedureMovment.shift;
            existingProcedureMovment.procedureType = procedureMovment.procedureType;

            // 3. تحديث وقت واسم آخر تعديل
            existingProcedureMovment.updatedOn = DateTime.Now;
            existingProcedureMovment.updatedBy = procedureMovment.updatedBy;

            // 4. إذا تم تغيير التصريح المرتبط بالحركة
            if (procedureMovment.permitId != Guid.Empty)
            {
                var permit = await _unitOfWork.Permit.GetByIdAsync(procedureMovment.permitId);
                if (permit == null)
                    throw new Exception("تصريح الإجراء غير موجود");
                existingProcedureMovment.permit = permit;
            }

            // 5. تحديث الحركة في قاعدة البيانات
            _unitOfWork.ProcedureMovment.Update(existingProcedureMovment);

            // 6. حفظ التغييرات
            await _unitOfWork.SaveAsync();
        }

    
    }
}
