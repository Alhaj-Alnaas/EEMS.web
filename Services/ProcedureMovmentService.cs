using Core.Entities;
using Core.Interfaces.Services;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;


namespace Services
{
    public class ProcedureMovmentService : IProcedureMovment
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProcedureMovmentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task InsertProcedureMovment(ProcedureMovment movement)
        {
             _unitOfWork.ProceduresMovment.Insert(movement);
            await _unitOfWork.SaveAsync();
        }

        public void DeleteProcedureMovment(ProcedureMovment procedureMovment)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ProcedureMovment>> GetByPermitIdAsync(Guid permitId)
        {
            var ProcedureList = await _unitOfWork.ProceduresMovment.GetQueryable()
             .Where(p => p.permitId == permitId).ToListAsync();
            return ProcedureList;


        }

    }

}
