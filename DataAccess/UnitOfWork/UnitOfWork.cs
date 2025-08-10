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
    public class UnitOfWork<T> : IUnitOfWork<T> where T : Base
    {
        private readonly DataContext _context;
        private IGenericRepository<T> _entity;
        protected readonly IUserProvider _userProvider;


        public UnitOfWork(
            DataContext context,
            IUserProvider userProvider)
        {
            _context = context;
            _userProvider = userProvider;
        }
        public IGenericRepository<T> Entity
        {
            get
            {
                return _entity ?? (_entity = new GenericRepository<T>(_context, _userProvider));
            }
        }

        public void Save()
        {
            _context.SaveChanges();
        }
    }
}
