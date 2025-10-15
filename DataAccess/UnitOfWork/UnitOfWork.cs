using Core.Entities;
using Core.Interfaces.Repositories;
using DataAccess.Repositories;
using EEMS.Core.Interfaces.Repositories;
using EEMS.Core.Interfaces.UnitOfWork;


namespace DataAccess.UnitOfWork
{
    public class UnitOfWork  : IUnitOfWork, IDisposable
    {
        private readonly DataContext _context;

        private IGenericRepository<Gate> _gates;
        private IGenericRepository<PermitType> _permitTypes;
        private IGenericRepository<Permit> _permits;
        IGenericRepository<ProcedureMovment> _proceduresMovment;
        private IStoredProcedureRepository _storedProcedures;
        
        public UnitOfWork(DataContext context)
        {
            _context = context;
           
        }

        public IGenericRepository<Gate> Gates => _gates ??= new GenericRepository<Gate>(_context);
        public IGenericRepository<PermitType> PermitTypes => _permitTypes ??= new GenericRepository<PermitType>(_context);
        public IGenericRepository<Permit> Permits => _permits ??= new GenericRepository<Permit>(_context);

        public IGenericRepository<ProcedureMovment> ProceduresMovment => _proceduresMovment ??= new GenericRepository<ProcedureMovment>(_context);

        public IStoredProcedureRepository StoredProcedures
        => _storedProcedures ??= new StoredProcedureRepository(_context);
        
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
