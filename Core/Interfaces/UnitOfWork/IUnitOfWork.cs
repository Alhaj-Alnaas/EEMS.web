using Core.Entities;
using EEMS.Core.Interfaces.Repositories;
using System.Threading.Tasks;

namespace EEMS.Core.Interfaces.UnitOfWork
{
    public interface IUnitOfWork
    {
        IGenericRepository<Gate> Gates { get; }
        IGenericRepository<PermitType> PermitTypes { get; }
        IGenericRepository<Permit> Permit { get; }
        IGenericRepository<EquipMatiMovment> EquipMatiMovment { get; }
        IGenericRepository<CarMovment> CarMovment { get; }
        IGenericRepository<ProcedureMovment> ProcedureMovment { get; }
        IGenericRepository<HumanMovment> HumanMovment { get; }



        Task SaveAsync();
    }
}
