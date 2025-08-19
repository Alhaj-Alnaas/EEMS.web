using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;


namespace Services
{
    public class GateServices : IGates
    {
        private readonly IUnitOfWork _unitOfWork;

        public GateServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<Gate>> GetAllAsync()
        {
            return await _unitOfWork.Gates.GetAllAsync();
        }

        public async Task<Gate?> GetByIdAsync(Guid id, bool includePermitTypes = false)
        {
            if (includePermitTypes)
            {
                return await _unitOfWork.Gates.GetByIdAsync(
                    id,
                    q => q.Include(g => g.PermitTypes));
            }

            return await _unitOfWork.Gates.GetByIdAsync(id);
        }

        public async Task InsertAsync(Gate gate, List<int> selectedPermitTypeIds)
        {
   
            var permitTypes = (await _unitOfWork.PermitTypes.GetAllAsync())
                .Where(p => selectedPermitTypeIds.Contains(p.Id))
                .ToList();

            gate.PermitTypes = permitTypes;

            _unitOfWork.Gates.Insert(gate);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(Gate gate, List<int> selectedPermitTypeIds)
        {
            var existingGate = await _unitOfWork.Gates.GetByIdAsync(gate.Id);
            if (existingGate == null) throw new Exception("البوابة غير موجودة");

            existingGate.no = gate.no;
            existingGate.description = gate.description;
            existingGate.isActive = gate.isActive;
            existingGate.remarks = gate.remarks;
            existingGate.updatedOn = DateTime.Now;
            existingGate.updatedBy = gate.updatedBy;

            // تحديث Many-to-Many PermitTypes
            var permitTypes = (await _unitOfWork.PermitTypes.GetAllAsync())
                .Where(p => selectedPermitTypeIds.Contains(p.Id))
                .ToList();

            existingGate.PermitTypes.Clear();
            foreach (var permit in permitTypes)
            {
                existingGate.PermitTypes.Add(permit);
            }

            _unitOfWork.Gates.Update(existingGate);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Gate gate)
        {
            _unitOfWork.Gates.Delete(gate);
            await _unitOfWork.SaveAsync();
        }
    }
}
