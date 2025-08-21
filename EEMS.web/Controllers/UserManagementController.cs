using Core.Entities;
using Core.Entities.DTOs;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class UserManagementController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementController(UserManager<User> userManager,
                                    RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

   
        [HttpGet]
        public async Task<JsonResult> GetEmployeeData(string empNo)
        {
        if (string.IsNullOrEmpty(empNo))
            return Json(new { error = "رقم الملف مطلوب" });

        // التحقق من وجود المستخدم مسبقًا في قاعدة البيانات
        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.FileNumber == empNo);

        if (existingUser != null)
        {
            return Json(new { exists = true });
        }
        // إذا لم يكن موجودًاكمستخدم API لجلب بيانات الموظف
        try
        {
                using var client = new HttpClient();
                var url = $"http://10.10.102.16:8080/api/Employee/{empNo}";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return Json(new { error = "لم يتم العثور على بيانات الموظف" });

                var data = await response.Content.ReadFromJsonAsync<EmployeeApiModel>();

            
            return Json(new
                {
                empName = data.empName,
                perRespCodeNoName = data.perRespCodeNoName,
                jobtypeName = data.jobtypeName,
                empEmail = data.empEmail,
                jobStatus = data.jobStatus,
                empPhone = data.empPhone,
                respCodeId = data.respCodeId,
                empResponsibilitycode = data.empResponsibilitycode,
                jobCatId = data.jobCatId,
                designationId = data.designationId
            });
            }
            catch (Exception ex)
            {
                return Json(new { error = "حدث خطأ أثناء جلب بيانات الموظف" });
            }
        }
    

    // نموذج البيانات المستلمة من الـ API
    public class EmployeeApiModel
    {
        public string empFileNo { get; set; }
        public string empName { get; set; }
        public string empPhone { get; set; }
        public string empEmail { get; set; }
        public string respCodeId { get; set; }
        public string empResponsibilitycode { get; set; }
        public string perRespCodeNoName { get; set; }
        public int jobCatId { get; set; }
        public string jobStatus { get; set; }
        public int designationId { get; set; }
        public string jobtypeName { get; set; }
    }


    // GET: CreateUser
    public IActionResult CreateUser()
    {
        var vm = new CreateUserViewModel();
        ViewData["Title"] = "إضافة مستخدم جديد";
        return View(vm);
    }

    // POST: CreateUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            //return View(vm);
        }

        if (string.IsNullOrEmpty(vm.EmpFileNo))
        {
            ModelState.AddModelError("EmpFileNo", "الرقم الوظيفي مطلوب");
            return View(vm);
        }

        if (string.IsNullOrEmpty(vm.UserType))
        {
            ModelState.AddModelError("UserType", "يجب اختيار تصنيف المستخدم");
            return View(vm);
        }

      
        // إنشاء كائن المستخدم الجديد
        var user = new User
        {
            UserName = vm.EmpFileNo,                  // رقم الموظف
            NormalizedUserName = vm.EmpEmail?.ToUpper(),
            Email = vm.EmpEmail,
            NormalizedEmail = vm.EmpEmail?.ToUpper(),
            PhoneNumber = vm.EmpPhone,
            FileNumber = vm.EmpFileNo,
            FullName = vm.EmpName,
            ResponsibilityCode = vm.EmpResponsibilityCode,
            Department = vm.Department,
            RespCodeId = vm.RespCodeId ?? 0,
            JobCatId = vm.JobCatId ?? 0,
            JobStatus = vm.JobStatus,
            DesignationId = vm.DesignationId ?? 0,
            JobtypeName = vm.JobTypeName,
            Discriminator = vm.UserType,
            UserType = vm.UserType
        };

        // إنشاء المستخدم مع كلمة المرور
        var result = await _userManager.CreateAsync(user, vm.Password);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم إضافة المستخدم بنجاح!";
            return RedirectToAction("CreateUser");
        }

        // إضافة الأخطاء إلى ModelState لعرضها في الـ View
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(vm);
    }

    [HttpGet]
    public JsonResult CheckIfUserExists(string empFileNo)
    {
        if (string.IsNullOrEmpty(empFileNo))
            return Json(false);

        var exists = _userManager.Users.Any(u => u.FileNumber == empFileNo);
        return Json(exists);
    }

   

}
