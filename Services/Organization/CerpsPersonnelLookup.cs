using System.Data;
using System.Globalization;
using Core.Interfaces.Services.Organization;
using Core.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.Organization
{
    public class CerpsPersonnelLookup : ICerpsPersonnelLookup
    {
        public const string NotRegisteredMessage =
            "هذا الموظف غير مسجل في قاعدة بيانات شؤون العاملين نأمل مراجعة الجهات المختصة";

        private readonly IDbContextFactory<DataContext> _factory;

        public CerpsPersonnelLookup(IDbContextFactory<DataContext> factory)
        {
            _factory = factory;
        }

        public async Task<HrEmployeeLookupResult> LookupAsync(string fileNumber, CancellationToken ct = default)
        {
            var pin = (fileNumber ?? "").Trim();
            if (!decimal.TryParse(pin, NumberStyles.Number, CultureInfo.InvariantCulture, out var empNo))
            {
                return new HrEmployeeLookupResult { Success = false, Error = NotRegisteredMessage };
            }

            try
            {
                await using var db = await _factory.CreateDbContextAsync(ct);
                var conn = db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 40;
                cmd.CommandText = """
                    SELECT TOP 1
                        CAST(emp_fileno AS varchar(20)),
                        emp_name,
                        jobtype_name,
                        emp_bloodgroup,
                        emp_nationality,
                        emp_address,
                        emp_city,
                        emp_phone,
                        emp_email,
                        emp_responsibilitycode,
                        PerRespCodeNoName,
                        DepartmentNameNo,
                        LTRIM(RTRIM(job_status)),
                        status_desc_ar
                    FROM [10.10.101.4].CERPS_Hosted.dbo.v_per_m_all_emp
                    WHERE emp_fileno = @no AND ISNULL(deletion_flag, 0) = 0
                    """;
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = "@no";
                parameter.Value = empNo;
                cmd.Parameters.Add(parameter);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    return new HrEmployeeLookupResult { Success = false, Error = NotRegisteredMessage };

                return new HrEmployeeLookupResult
                {
                    Success = true,
                    FileNumber = Text(reader, 0) ?? pin,
                    FullName = Text(reader, 1),
                    JobTitle = Text(reader, 2),
                    BloodType = Text(reader, 3),
                    Nationality = Text(reader, 4),
                    Address = Text(reader, 5),
                    City = Text(reader, 6),
                    Phone = Text(reader, 7),
                    Email = Text(reader, 8),
                    ResponsibilityCode = Text(reader, 9),
                    DepartmentName = Text(reader, 10),
                    AdministrationName = Text(reader, 11),
                    JobStatus = Text(reader, 12),
                    JobStatusName = Text(reader, 13)
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new HrEmployeeLookupResult
                {
                    Success = false,
                    Error = "تعذر الاتصال ببيانات شؤون الأفراد: " + ex.Message
                };
            }
        }

        private static string? Text(IDataRecord reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            var value = Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
