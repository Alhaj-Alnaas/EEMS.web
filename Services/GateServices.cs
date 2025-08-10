using Core.Entities;
using EEMS.Core.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Core.Interfaces.Services;

namespace Services
{
    public class GateServices<T> : IGates<T> where T: Gate
    {
        private readonly IUnitOfWork<Gate> _unitOfWork;
       public  GateServices(IUnitOfWork<Gate> unitOfWork) {
            _unitOfWork = unitOfWork;   
              } 
        public void DeleteGate(Gate gate)
        {
            _unitOfWork.Entity.Delete(gate);
            _unitOfWork .Save();
        }

        public List<Gate> GetAllGates()
        {
            throw new NotImplementedException();
        }

        public List<Gate> GetGatesByUsing(in string PemitType)
        {
            throw new NotImplementedException();
        }

        public void InsertGate(Gate gate)
        {
            _unitOfWork .Entity.Insert(gate);
            _unitOfWork .Save ();
        }

        public void UpdateGate(Gate gate)
        {
            _unitOfWork.Entity.Update(gate);
            _unitOfWork.Save();
        }
    }
}
