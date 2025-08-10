using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface ICarMovment<T> where T : class
    {
        void InsertCarMovment(CarMovment carMovment);
        void UpdateCarMovment(CarMovment carMovment);
        void DeleteCarMovment(CarMovment carMovment);
        List <string> SearchByCarMovment(CarMovment carMovment);
        List<string> GetCarMovment(CarMovment carMovment);

    }
}
