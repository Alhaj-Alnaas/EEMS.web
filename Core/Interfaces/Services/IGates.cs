using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces.Services
{
    public interface IGates
    {
        Task<IEnumerable<Gate>> GetAllAsync();
        Task<Gate> GetByIdAsync(Guid id);
        Task InsertAsync(Gate gate, List<int> selectedPermitTypeIds);
        Task UpdateAsync(Gate gate, List<int> selectedPermitTypeIds);
        Task DeleteAsync(Gate gate);
    }
}
