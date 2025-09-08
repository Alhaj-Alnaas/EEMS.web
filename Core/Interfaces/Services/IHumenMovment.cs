using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces.Services
{
    public interface IHumenMovment
    {

        Task InsertHumenMovment( HumanMovment humenMovment);
        Task UpdateHumenMovment( HumanMovment humenMovment);
        Task DeleteHumenMovment( HumanMovment humenMovment);

        Task<List<HumanMovment>> SearchByAsync(List<Entities.Parameter> parameters);
        Task<List<HumanMovment>> GetHumenMovment(string PermitId);
    }
}
