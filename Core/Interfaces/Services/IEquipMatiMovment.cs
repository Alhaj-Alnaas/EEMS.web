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
        void InsertEquipMatiMovment( EquipMatiMovment equipMatiMovment);
        void UpdateEquipMatiMovment(EquipMatiMovment equipMatiMovment);
        void DeleteEquipMatiMovment(EquipMatiMovment equipMatiMovment);
        List<string> EquipMatiMovmentSearchBy(EquipMatiMovment equipMatiMovment);
        List<string> GetEquipMatiMovment(EquipMatiMovment equipMatiMovment);




    }
}
