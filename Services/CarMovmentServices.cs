using Core.Entities;
using EEMS.Core.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Core.Interfaces.Services;

namespace Services
{
    public class CarMovmentServices : ICarMovment
    {
        private readonly IUnitOfWork _unitOfWork;

        public CarMovmentServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task DeleteCarMovment(CarMovment carMovment)
        {
            if (carMovment == null) throw new ArgumentNullException(nameof(carMovment));

            _unitOfWork.CarMovment.Delete(carMovment);
            await _unitOfWork.SaveAsync();
        }


        public List<string> GetCarMovment(Permit permit)

        {
            // جلب كل التحركات المتعلقة بنفس التصريح (permitId).
            // The FindAsync method is now awaited correctly.
            var result = _unitOfWork.CarMovment.GetAllAsync().Result.Where(c => c.Id == permit.Id);

            // Project the results into a list of formatted strings.
            return result.Select(c =>
                $"CarNo: {c.carNo}, Driver: {c.driverOrg}, Nationality: {c.driverNationality}, License: {c.licenseNo}"
            ).ToList();
        }


        public async Task InsertCarMovment(CarMovment carMovment)
        {
            if (carMovment == null) throw new ArgumentNullException(nameof(carMovment));

            _unitOfWork.CarMovment.Insert(carMovment);
            await _unitOfWork.SaveAsync();
        }

        public List<string> SearchByCarMovment(CarMovment carMovment)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateCarMovment(CarMovment carMovment)
        {
            // جلب الحركة الأصلية مع الـ Permit
            var existingCarMovment = await _unitOfWork.CarMovment.GetByIdAsync(carMovment.Id);
            if (existingCarMovment == null) throw new Exception("حركة السيارة غير موجودة");

            // تحديث الحقول
            existingCarMovment.permitId = carMovment.permitId;
            existingCarMovment.carType = carMovment.carType;
            existingCarMovment.carNo = carMovment.carNo;
            existingCarMovment.driverId = carMovment.driverId;
            existingCarMovment.driverOrg = carMovment.driverOrg;
            existingCarMovment.driverNationality = carMovment.driverNationality;
            existingCarMovment.licenseNo = carMovment.licenseNo;
            existingCarMovment.updatedOn = DateTime.Now;
            existingCarMovment.updatedBy = carMovment.updatedBy;

            // جلب الـ Permit إذا احتجنا لتحديث العلاقة
            if (carMovment.permitId != Guid.Empty)
            {
                var permit = await _unitOfWork.Permit.GetByIdAsync(carMovment.permitId);
                if (permit == null) throw new Exception("تصريح السيارة غير موجود");
                existingCarMovment.permit = permit;
            }

            _unitOfWork.CarMovment.Update(existingCarMovment);
            await _unitOfWork.SaveAsync();
        }


        public async Task<IEnumerable<CarMovment>> GetAllAsync()
        {
            return await _unitOfWork.CarMovment.GetAllAsync();
        }

        public List<string> GetCarMovment(CarMovment carMovment)
        {
            throw new NotImplementedException();
        }
    }
}
