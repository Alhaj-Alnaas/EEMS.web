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
        Task<Gate?> GetByIdAsync(Guid id);
        Task InsertAsync(Gate gate);
        Task UpdateAsync(Gate gate);
        Task DeleteAsync(Gate gate);
    }
}
