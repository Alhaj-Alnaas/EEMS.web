using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces.Services
{
    public interface IHumenMovment<T> where T : class
    {

        void InsertHumenMovment( HumanMovment humenMovment);
        void UpdateHumenMovment( HumanMovment humenMovment);
        void DeleteHumenMovment( HumanMovment humenMovment);
        List <HumanMovment> SearchBy(List<Parameter> parameters);
        List<HumanMovment> GetHumenMovment(in string PermitId);
    }
}
