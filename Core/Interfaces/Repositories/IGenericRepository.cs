
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Core.Interfaces.Repositories
{
    public  interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync(Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null);

        Task<T> GetByIdAsync(Guid id,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null);
        void Insert(T entity);
        void Update(T entity);
        void Delete(T entity);
        void PermanentDelete(T entity);

        IQueryable<T> GetQueryable(); // ??? ?????? ????????? ????? ??? ????? DB
        Task<List<T>> ExecuteStoredProcedureAsync<P>(string storedProcedure, params object[] parameters) where P : class;
    } 
}
