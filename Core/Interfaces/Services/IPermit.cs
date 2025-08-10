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
    public interface IPermit<T> where T : class
    {
        void InsertPermit(Permit permit);
        void UpdatePermit(Permit permit);
        void DeletePermit(Permit permit);
        Permit GetPermitById(string PermitId);
        Permit GetPermitByType( string permitType);
        List<Permit> GetUnApprovedPermit(string UserId);
        List<Permit> GetPendingPermit( string UserId, string RespCode);
        List<Permit> GetClosedPermit( string UserId);
        List<Permit> GetAllPermit( string UserId);
        List<Permit> SearchBy(List<Parameter> parameters);
        string GeneratePermitSerialNumber( string permitType);

    }
}
