using Core.Entities;
using Core.Entities.DTOs;
using Core.Interfaces.Repositories;
using EEMS.Core.Interfaces.Repositories;
using System.Threading.Tasks;

namespace EEMS.Core.Interfaces.UnitOfWork
{
    public interface IUnitOfWork
    {
        IGenericRepository<Gate> Gates { get; }
        IGenericRepository<PermitType> PermitTypes { get; }
        IGenericRepository<Permit> Permits { get; }

        IStoredProcedureRepository StoredProcedures { get; }

        Task SaveAsync();
    }
}
