using Core.Interfaces.Services.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.web.Controllers
{
    /// <summary>
    /// HR employee lookup for the Blazor employee-registration screen.
    /// Same external API as UserManagement, exposed as an explicit JSON endpoint.
    /// </summary>
    [ApiController]
    [Route("api/hr/employee")]
    [AllowAnonymous]
    public class HrEmployeeController : ControllerBase
    {
        private readonly IHrEmployeeLookupService _hrLookup;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<HrEmployeeController> _logger;

        public HrEmployeeController(
            IHrEmployeeLookupService hrLookup,
            IEmployeeService employeeService,
            ILogger<HrEmployeeController> logger)
        {
            _hrLookup = hrLookup;
            _employeeService = employeeService;
            _logger = logger;
        }

        [HttpGet("{empNo}")]
        public async Task<IActionResult> Get(string empNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(empNo))
                return Ok(new { error = "رقم الملف مطلوب" });

            try
            {
                var data = await _hrLookup.LookupAsync(empNo.Trim(), ct);
                if (!data.Success)
                    return Ok(new { error = data.Error ?? "لم يتم العثور على بيانات الموظف" });

                bool alreadyInSystem = false;
                try
                {
                    var existing = await _employeeService.GetByFileNumberAsync(empNo.Trim());
                    alreadyInSystem = existing != null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check Employees table for {EmpNo}", empNo);
                }

                return Ok(new
                {
                    empFileNo = data.FileNumber,
                    empName = data.FullName,
                    perRespCodeNoName = data.DepartmentName,
                    jobtypeName = data.JobTitle,
                    empEmail = data.Email,
                    jobStatus = data.JobStatus,
                    empPhone = data.Phone,
                    respCodeId = data.RespCodeId?.ToString(),
                    empResponsibilitycode = data.ResponsibilityCode,
                    jobCatId = data.JobCatId,
                    designationId = data.DesignationId,
                    empAddress = data.Address,
                    address = data.Address,
                    nationality = data.Nationality,
                    empNationality = data.Nationality,
                    bloodType = data.BloodType,
                    bloodGroup = data.BloodType,
                    alreadyInSystem
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR API proxy failed for {EmpNo}", empNo);
                return Ok(new { error = "حدث خطأ أثناء جلب بيانات الموظف: " + ex.Message });
            }
        }
    }
}
