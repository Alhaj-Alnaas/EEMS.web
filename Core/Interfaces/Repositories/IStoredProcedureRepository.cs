using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Repositories
{
    public interface IStoredProcedureRepository
    {
        Task<List<T>> ExecuteStoredProcedureAsync<T>(string storedProcedure, params object[] parameters) where T : class;
    }
}
