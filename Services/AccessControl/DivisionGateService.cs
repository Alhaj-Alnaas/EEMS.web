using System.Data;
using Core.Interfaces.Services.AccessControl;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.AccessControl
{
    public class DivisionGateService : IDivisionGateService
    {
        private readonly IDbContextFactory<DataContext> _factory;

        public DivisionGateService(IDbContextFactory<DataContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<Guid>> GetGateIdsAsync(string responsibilityCode, CancellationToken ct = default)
        {
            var code = Normalize(responsibilityCode);
            if (code.Length == 0)
                return new List<Guid>();

            await using var db = await _factory.CreateDbContextAsync(ct);
            await EnsureTableAsync(db, ct);
            var conn = await OpenAsync(db, ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GateId FROM DivisionGateLinks WHERE ResponsibilityCode = @code";
            Add(cmd, "@code", code);
            var ids = new List<Guid>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(reader.GetGuid(0));
            return ids;
        }

        public async Task ReplaceAsync(string responsibilityCode, IReadOnlyCollection<Guid> gateIds, CancellationToken ct = default)
        {
            var code = Normalize(responsibilityCode);
            if (code.Length == 0)
                return;

            await using var db = await _factory.CreateDbContextAsync(ct);
            await EnsureTableAsync(db, ct);
            var conn = await OpenAsync(db, ct);
            await using var delete = conn.CreateCommand();
            delete.CommandText = "DELETE FROM DivisionGateLinks WHERE ResponsibilityCode = @code";
            Add(delete, "@code", code);
            await delete.ExecuteNonQueryAsync(ct);

            foreach (var gateId in gateIds.Distinct())
            {
                await using var insert = conn.CreateCommand();
                insert.CommandText = "INSERT INTO DivisionGateLinks (Id, ResponsibilityCode, GateId) VALUES (@id, @code, @gate)";
                Add(insert, "@id", Guid.NewGuid());
                Add(insert, "@code", code);
                Add(insert, "@gate", gateId);
                await insert.ExecuteNonQueryAsync(ct);
            }
        }

        private static async Task EnsureTableAsync(DataContext db, CancellationToken ct)
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'DivisionGateLinks', N'U') IS NULL
                BEGIN
                    CREATE TABLE DivisionGateLinks (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_DivisionGateLinks PRIMARY KEY,
                        ResponsibilityCode nvarchar(5) NOT NULL,
                        GateId uniqueidentifier NOT NULL
                    );
                    CREATE UNIQUE INDEX IX_DivisionGateLinks_Code_Gate
                        ON DivisionGateLinks (ResponsibilityCode, GateId);
                END
                """, ct);
        }

        private static async Task<System.Data.Common.DbConnection> OpenAsync(DataContext db, CancellationToken ct)
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);
            return conn;
        }

        private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }

        private static string Normalize(string? code)
        {
            var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
            return digits.Length >= 5 ? digits[..5] : digits;
        }
    }
}
