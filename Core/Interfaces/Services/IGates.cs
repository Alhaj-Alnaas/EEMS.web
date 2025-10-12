using Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IGates
    {
        Task<IEnumerable<Gate>> GetAllAsync();
        Task<Gate> GetByIdAsync(Guid id, bool includePermitTypes);
        Task InsertAsync(Gate gate, List<int> selectedPermitTypeIds);
        Task UpdateAsync(Gate gate, List<int> selectedPermitTypeIds);
        Task DeleteAsync(Gate gate);

        Task<List<Gate>> GetGatesByPermitTypeAsync(int permitTypeId);
    }
}
