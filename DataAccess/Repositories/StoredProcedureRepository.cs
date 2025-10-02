using Core.Interfaces.Repositories;
using DataAccess;
using Microsoft.EntityFrameworkCore;

public class StoredProcedureRepository : IStoredProcedureRepository
{
    private readonly DataContext _context;

    public StoredProcedureRepository(DataContext context)
    {
        _context = context;
    }

    public async Task<List<T>> ExecuteStoredProcedureAsync<T>(string storedProcedure, params object[] parameters) where T : class
    {
        return await _context.Set<T>()
            .FromSqlRaw(storedProcedure, parameters)
            .ToListAsync();
    }
}
