using EEMS.Core.Interfaces.Repositories;
using System.Threading.Tasks;

namespace EEMS.Core.Interfaces.UnitOfWork
{
    public interface IUnitOfWork<T> where T : class
    {
        IGenericRepository <T> Entity { get; }
        void Save();
    }
}
