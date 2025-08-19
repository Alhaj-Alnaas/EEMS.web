using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class PermitTypeServices : IPermitType
    {
        private readonly IUnitOfWork _unitOfWork;

        public PermitTypeServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<PermitType>> GetAllAsync()
        {
            return await _unitOfWork.PermitTypes.GetAllAsync();
        }

        //public async Task<PermitType> GetByIdAsync(int id)
        //{
        //    return await _unitOfWork.PermitTypes.GetByIdAsync(id);
        //}

        public async Task InsertAsync(PermitType permitType)
        {
            _unitOfWork.PermitTypes.Insert(permitType);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(PermitType permitType)
        {
            _unitOfWork.PermitTypes.Update(permitType);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(PermitType permitType)
        {
            _unitOfWork.PermitTypes.Delete(permitType);
            await _unitOfWork.SaveAsync();
        }
    }
}
