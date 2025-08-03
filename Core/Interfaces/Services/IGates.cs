using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Entities;

namespace Core.Interfaces.Services
{
    public interface IGates
    {
        void InsertGate( Gate gate);
        void UpdateGate( Gate gate);
        void DeleteGate( Gate gate );
        List<Gate> GetAllGates();
        List<Gate> GetGatesByUsing(in string PemitType);
    }
}
