using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IEquipMatiMovment
    {
        Task InsertEquipMatiMovment( EquipMatiMovment equipMatiMovment);
        Task UpdateEquipMatiMovment(EquipMatiMovment equipMatiMovment);
        Task DeleteEquipMatiMovment(EquipMatiMovment equipMatiMovment);
        List<string> EquipMatiMovmentSearchBy(EquipMatiMovment equipMatiMovment);
        List<string> GetEquipMatiMovment(EquipMatiMovment equipMatiMovment);




    }
}
