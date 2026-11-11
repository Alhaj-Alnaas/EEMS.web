using System.Data;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.UnitOfWork;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public class GateServices : IGates
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public GateServices(IUnitOfWork unitOfWork, IDbContextFactory<DataContext> dbFactory)
        {
            _unitOfWork = unitOfWork;
            _dbFactory = dbFactory;
        }

        public async Task<IEnumerable<Gate>> GetAllAsync()
        {
            return await _unitOfWork.Gates.GetAllAsync();
        }

        public async Task<Gate?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Gates.GetByIdAsync(id);
        }

        public async Task InsertAsync(Gate gate)
        {
            _unitOfWork.Gates.Insert(gate);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(Gate gate)
        {
            var existingGate = await _unitOfWork.Gates.GetByIdAsync(gate.Id);
            if (existingGate == null)
                throw new Exception("البوابة غير موجودة.");

            existingGate.no = gate.no;
            existingGate.description = gate.description;
            existingGate.isActive = gate.isActive;
            existingGate.remarks = gate.remarks;
            existingGate.updatedOn = DateTime.Now;
            existingGate.updatedBy = gate.updatedBy;

            _unitOfWork.Gates.Update(existingGate);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Gate gate)
        {
            var id = gate.Id;
            var readers = await _unitOfWork.Readers.GetQueryable()
                .CountAsync(r => r.GateId == id && !r.isDeleted);
            if (readers > 0)
                throw new InvalidOperationException($"لا يمكن حذف البوابة لارتباطها بـ {readers} قارئة.");

            var permissions = await _unitOfWork.EmployeePermissions.GetQueryable()
                .CountAsync(p => p.GateId == id && p.IsActive && !p.isDeleted);
            if (permissions > 0)
                throw new InvalidOperationException($"لا يمكن حذف البوابة لارتباطها بصلاحيات {permissions} موظف.");

            var movements = await _unitOfWork.MovementLogs.GetQueryable()
                .CountAsync(m => m.GateId == id && !m.isDeleted);
            if (movements > 0)
                throw new InvalidOperationException("لا يمكن حذف البوابة لأن لها سجل حركة في النظام.");

            var permits = await _unitOfWork.Permits.GetQueryable()
                .CountAsync(p => p.GateId == id && !p.isDeleted);
            if (permits > 0)
                throw new InvalidOperationException("لا يمكن حذف البوابة لارتباطها بتصاريح مسجلة.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var classLinks = await db.PermitClassificationGates.CountAsync(x => x.GateId == id);
            if (classLinks > 0)
                throw new InvalidOperationException("لا يمكن حذف البوابة لأنها مربوطة بأنواع تصاريح في إعداد التصريح.");

            if (await CountDivisionLinksAsync(db, id) > 0)
                throw new InvalidOperationException("لا يمكن حذف البوابة لأنها مربوطة بتقسيم إداري.");

            _unitOfWork.Gates.Delete(gate);
            await _unitOfWork.SaveAsync();
        }

        private static async Task<int> CountDivisionLinksAsync(DataContext db, Guid gateId)
        {
            var conn = db.Database.GetDbConnection();
            var opened = conn.State != ConnectionState.Open;
            if (opened) await conn.OpenAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    IF OBJECT_ID(N'dbo.DivisionGateLinks', N'U') IS NULL
                        SELECT 0;
                    ELSE
                        SELECT COUNT(*) FROM dbo.DivisionGateLinks WHERE GateId = @id;
                    """;
                var p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = gateId;
                cmd.Parameters.Add(p);
                var value = await cmd.ExecuteScalarAsync();
                return value == null || value is DBNull ? 0 : Convert.ToInt32(value);
            }
            finally
            {
                if (opened) await conn.CloseAsync();
            }
        }

    }
}
