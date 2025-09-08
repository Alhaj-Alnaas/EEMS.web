using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public class EquipMatiMovmentServices : IEquipMatiMovment

    {
        private readonly IUnitOfWork _unitOfWork;

        public EquipMatiMovmentServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task DeleteEquipMatiMovment(EquipMatiMovment equipMatiMovment)
        {
            if (equipMatiMovment == null) throw new ArgumentNullException(nameof(equipMatiMovment));

            _unitOfWork.EquipMatiMovment.Delete(equipMatiMovment);
            await _unitOfWork.SaveAsync();
        }


        public List<string> EquipMatiMovmentSearchBy(EquipMatiMovment equipMatiMovment)
        {
            throw new NotImplementedException();
        }

        public List<string> GetEquipMatiMovment(EquipMatiMovment equipMatiMovment)
        {
            throw new NotImplementedException();
        }

        public async Task InsertEquipMatiMovment(EquipMatiMovment equipMatiMovment)
        {
            if (equipMatiMovment == null) throw new ArgumentNullException(nameof(equipMatiMovment));

            _unitOfWork.EquipMatiMovment.Insert(equipMatiMovment);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateEquipMatiMovment(EquipMatiMovment equipMatiMovment)
        {
            if (equipMatiMovment == null) throw new ArgumentNullException(nameof(equipMatiMovment));
            // جلب الحركة الأصلية مع الـ Permit
            var existingEquipMatiMovment = await _unitOfWork.EquipMatiMovment.GetByIdAsync(equipMatiMovment.Id);
            if (existingEquipMatiMovment == null) throw new KeyNotFoundException("حركة المعدات/المواد غير موجودة");

            // تحديث الحقول
            existingEquipMatiMovment.permitId = equipMatiMovment.permitId;
            existingEquipMatiMovment.description = equipMatiMovment.description;
            existingEquipMatiMovment.qty = equipMatiMovment.qty;
            existingEquipMatiMovment.returnDate = equipMatiMovment.returnDate;
            existingEquipMatiMovment.updatedOn = DateTime.Now;
            existingEquipMatiMovment.updatedBy = equipMatiMovment.updatedBy;

            // جلب الـ Permit إذا احتجنا لتحديث العلاقة
            if (equipMatiMovment.permitId != Guid.Empty)
            {
                var permit = await _unitOfWork.Permit.GetByIdAsync(equipMatiMovment.permitId);
                if (permit == null) throw new KeyNotFoundException("تصريح المعدات/المواد غير موجود");
                existingEquipMatiMovment.permit = permit;
            }

            // تحديث وحفظ
            _unitOfWork.EquipMatiMovment.Update(existingEquipMatiMovment);
            await _unitOfWork.SaveAsync();
        }

    }
}
