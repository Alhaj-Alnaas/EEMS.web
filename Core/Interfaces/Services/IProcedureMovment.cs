using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IProcedureMovment
    {
         Task InsertProcedureMovment(ProcedureMovment procedureMovment);
        Task<List<ProcedureMovment>> GetByPermitIdAsync(Guid permitId);

        //void UpdateProcedureMovment(ProcedureMovment procedureMovment);
        //void DeleteProcedureMovment(ProcedureMovment procedureMovment);
        //List<string> SearchByProcedureMovment(ProcedureMovment procedureMovment);
        //List<string> GetProcedureMovment(ProcedureMovment procedureMovment);
    }
}
