using Core.Entities;
using EEMS.Core.Interfaces.Repositories;
using System.Threading.Tasks;

namespace EEMS.Core.Interfaces.UnitOfWork
{
    public interface IUnitOfWork
    {
        IGenericRepository<Gate> Gates { get; }
        IGenericRepository<PermitType> PermitTypes { get; }

        Task SaveAsync();
    }
}
