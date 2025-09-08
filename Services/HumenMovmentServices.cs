using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
namespace Services
{
    public class HumenMovmentServices : IHumenMovment
    {
        private readonly IUnitOfWork _unitOfWork;

        public HumenMovmentServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // إدراج حركة إنسانية
        public async Task InsertHumenMovment(HumanMovment humenMovment)
        {
            if (humenMovment == null)
                throw new ArgumentNullException(nameof(humenMovment));

            _unitOfWork.HumanMovment.Insert(humenMovment);
            await _unitOfWork.SaveAsync();
        }

        // تحديث حركة إنسانية
        public async Task UpdateHumenMovment(HumanMovment humenMovment)
        {
            var existingHumenMovment = await _unitOfWork.HumanMovment.GetByIdAsync(humenMovment.Id);
            if (existingHumenMovment == null)
                throw new Exception("الحركة غير موجودة");

            // تحديث الحقول الأساسية
            existingHumenMovment.permitId = humenMovment.permitId;
            existingHumenMovment.orgToVisit = humenMovment.orgToVisit;
            existingHumenMovment.personToVist = humenMovment.personToVist;
            existingHumenMovment.purposeOfVisit = humenMovment.purposeOfVisit;
            existingHumenMovment.phoneNo = humenMovment.phoneNo;

            // تحديث وقت واسم آخر تعديل
            existingHumenMovment.updatedOn = DateTime.Now;
            existingHumenMovment.updatedBy = humenMovment.updatedBy;

            // تحديث التصريح المرتبط إذا تم تغييره
            if (humenMovment.permitId != Guid.Empty)
            {
                var permit = await _unitOfWork.Permit.GetByIdAsync(humenMovment.permitId);
                if (permit == null)
                    throw new Exception("تصريح الحركة غير موجود");
                existingHumenMovment.permit = permit;
            }

            _unitOfWork.HumanMovment.Update(existingHumenMovment);
            await _unitOfWork.SaveAsync();
        }

        // حذف حركة إنسانية
        public async Task DeleteHumenMovment(HumanMovment humenMovment)
        {
            _unitOfWork.HumanMovment.Delete(humenMovment);
            await _unitOfWork.SaveAsync();
        }

        // جلب جميع الحركات المرتبطة بتصريح معين
        public async Task<List<HumanMovment>> GetHumenMovment( string PermitId)
        {
            var all = await _unitOfWork.HumanMovment.GetAllAsync();
            return all.Where(h => h.permitId.ToString() == PermitId).ToList();
        }


        // البحث باستخدام مجموعة من المعايير
        public async Task<List<HumanMovment>> SearchByAsync(List<Core.Entities.Parameter> parameters)
        {
            // جلب جميع السجلات من قاعدة البيانات وتحويلها إلى IQueryable للتمكن من تطبيق الفلاتر
            var query = (await _unitOfWork.HumanMovment.GetAllAsync()).AsQueryable();

          foreach (var param in parameters.Where(p => !string.IsNullOrWhiteSpace(p.Value)))
{
    query = param.Name switch
    {
        nameof(HumanMovment.orgToVisit)      => query.Where(h => h.orgToVisit.Contains(param.Value)),
        nameof(HumanMovment.personToVist)   => query.Where(h => h.personToVist.Contains(param.Value)),
        nameof(HumanMovment.purposeOfVisit) => query.Where(h => h.purposeOfVisit.Contains(param.Value)),
        _ => query
    };
}


            return query.ToList();
        }

    }
}
