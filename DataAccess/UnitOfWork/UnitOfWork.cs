using Core.Entities;
using Core.Interfaces.Services;
using DataAccess.Repositories;
using EEMS.Core.Interfaces.Repositories;
using EEMS.Core.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.UnitOfWork
{
    public class UnitOfWork  : IUnitOfWork, IDisposable
    {
        private readonly DataContext _context;

        private IGenericRepository<Gate> _gates;
        private IGenericRepository<PermitType> _permitTypes;


        public UnitOfWork(DataContext context)
        {
            _context = context;
        }

        public IGenericRepository<Gate> Gates => _gates ??= new GenericRepository<Gate>(_context);
        public IGenericRepository<PermitType> PermitTypes => _permitTypes ??= new GenericRepository<PermitType>(_context);
        
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
