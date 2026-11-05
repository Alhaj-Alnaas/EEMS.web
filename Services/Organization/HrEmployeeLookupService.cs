using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Interfaces.Services.Organization;
using Core.Models;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services.Organization
{
    public class HrEmployeeLookupService : IHrEmployeeLookupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private static readonly string[] AddressKeys =
        {
            "Address", "empAddress", "address", "homeAddress", "residenceAddress", "empHomeAddress",
            "addressAr", "empAddressAr", "residentialAddress"
        };

        private static readonly string[] NationalityKeys =
        {
            "Nationality", "nationality", "empNationality", "nation", "nationalityName", "empNation"
        };

        private static readonly string[] BloodTypeKeys =
        {
            "BloodType", "bloodType", "bloodGroup", "empBloodType", "blood", "bloodTyp", "empBloodGroup"
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HrEmployeeApiOptions _options;
        private readonly ILogger<HrEmployeeLookupService> _logger;

        public HrEmployeeLookupService(
            IHttpClientFactory httpClientFactory,
            IOptions<HrEmployeeApiOptions> options,
            ILogger<HrEmployeeLookupService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<HrEmployeeLookupResult> LookupAsync(string fileNumber, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileNumber))
            {
                return new HrEmployeeLookupResult
                {
                    Success = false,
                    Error = "الرقم الوظيفي مطلوب"
                };
            }

            var empNo = fileNumber.Trim();
            try
            {
                var client = _httpClientFactory.CreateClient("HrEmployeeApi");
                var url = BuildEmployeeUrl(empNo);
                _logger.LogInformation("HR lookup GET {Url}", url);
                using var response = await client.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HR lookup HTTP {Status} for {FileNumber}", (int)response.StatusCode, empNo);
                    return new HrEmployeeLookupResult
                    {
                        Success = false,
                        Error = response.StatusCode == System.Net.HttpStatusCode.NotFound
                            ? "لم يتم العثور على بيانات الموظف"
                            : $"تعذر الاتصال بخدمة الموارد البشرية ({(int)response.StatusCode})"
                    };
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new HrEmployeeLookupResult
                    {
                        Success = false,
                        Error = "لم يتم العثور على بيانات الموظف"
                    };
                }

                var empName = GetString(root, "empName");
                var empFileNo = GetString(root, "empFileNo");
                if (string.IsNullOrWhiteSpace(empName) && string.IsNullOrWhiteSpace(empFileNo))
                {
                    return new HrEmployeeLookupResult
                    {
                        Success = false,
                        Error = "لم يتم العثور على بيانات الموظف"
                    };
                }

                var jobStatus = GetString(root, "jobStatus")?.Trim();
                if (!string.Equals(jobStatus, "AE", StringComparison.OrdinalIgnoreCase))
                {
                    return new HrEmployeeLookupResult
                    {
                        Success = false,
                        Error = "لا يمكن عرض بيانات هذا الموظف. موظف غير مفعل",
                        FileNumber = string.IsNullOrWhiteSpace(empFileNo) ? empNo : empFileNo.Trim(),
                        JobStatus = jobStatus
                    };
                }

                var address = FirstString(root, AddressKeys);
                var nationality = FirstString(root, NationalityKeys);
                var bloodType = FirstString(root, BloodTypeKeys);

                if (address == null && nationality == null && bloodType == null)
                {
                    _logger.LogWarning(
                        "HR payload for {FileNumber} still has no address/nationality/bloodType after fields query (keys: {Keys})",
                        empNo,
                        string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
                }
                else
                {
                    _logger.LogInformation(
                        "HR extras for {FileNumber}: Address={HasAddress} Nationality={HasNat} BloodType={HasBlood}",
                        empNo,
                        !string.IsNullOrWhiteSpace(address),
                        !string.IsNullOrWhiteSpace(nationality),
                        !string.IsNullOrWhiteSpace(bloodType));
                }

                return new HrEmployeeLookupResult
                {
                    Success = true,
                    FileNumber = string.IsNullOrWhiteSpace(empFileNo) ? empNo : empFileNo.Trim(),
                    FullName = empName?.Trim(),
                    DepartmentName = GetString(root, "perRespCodeNoName")?.Trim(),
                    JobTitle = GetString(root, "jobtypeName")?.Trim(),
                    Email = GetString(root, "empEmail")?.Trim(),
                    Phone = GetString(root, "empPhone")?.Trim(),
                    JobStatus = jobStatus,
                    ResponsibilityCode = GetString(root, "empResponsibilitycode")?.Trim(),
                    RespCodeId = ParseFlexibleInt(root, "respCodeId"),
                    JobCatId = ParseFlexibleInt(root, "jobCatId"),
                    DesignationId = ParseFlexibleInt(root, "designationId"),
                    Address = address?.Trim(),
                    Nationality = nationality?.Trim(),
                    BloodType = bloodType?.Trim()
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HR employee lookup failed for {FileNumber}", empNo);
                return new HrEmployeeLookupResult
                {
                    Success = false,
                    Error = "حدث خطأ أثناء جلب بيانات الموظف: " + ex.Message
                };
            }
        }

        private string BuildEmployeeUrl(string empNo)
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(empNo)}";
            var fields = _options.ExtraFields?.Trim();
            if (string.IsNullOrWhiteSpace(fields))
                return url;

            return $"{url}?fields={Uri.EscapeDataString(fields)}";
        }

        private static string? FirstString(JsonElement root, IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                var value = GetString(root, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var prop))
                return null;

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static int? ParseFlexibleInt(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var element))
                return null;

            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n))
                return n;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var s))
                return s;
            return null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
        {
            if (root.TryGetProperty(name, out value))
                return true;

            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
