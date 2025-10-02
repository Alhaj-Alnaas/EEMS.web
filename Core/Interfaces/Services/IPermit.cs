using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces.Services
{
    public interface IPermit
    {
        Task InsertPermitAsync(Permit permit);
        Task UpdatePermitAsync(Permit permit);
        Task DeletePermitAsync(Permit permit);
        Task <Permit> GetPermitByIdAsync(Guid PermitId);
        Task<Permit> GetPermitByTypeAsync( string permitType);
       Task <List<Permit>> GetUnApprovedPermitAsync(string UserId);
        Task<List<Permit>> GetPendingPermitAsync( string UserId, string RespCode);
        Task<List<Permit>> GetClosedPermitAsync( string UserId);
        Task<List<Permit>> GetAllPermitAsync(User user);
        Task<List<Permit>> SearchByAsync(List<Parameter> parameters);
         Task<string> GeneratePermitSerialNumberAsync( string permitType);
        
    }
}
