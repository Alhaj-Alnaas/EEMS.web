using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public  interface IPermitType
    {
        Task<IEnumerable<PermitType>> GetAllAsync();
        Task<PermitType> GetByIdAsync(int id);
        Task InsertAsync(PermitType permitType);
        Task UpdateAsync(PermitType permitType);
        Task DeleteAsync(PermitType permitType);

    }
}
