using Core.Entities;
using Core.Interfaces.Services.Organization;
using PMS.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
[Authorize]
public class UserManagementController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IHrEmployeeLookupService _hrLookup;

    public UserManagementController(UserManager<User> userManager,
                                    SignInManager<User> signInManager,
                                    RoleManager<IdentityRole> roleManager,
                                    IHrEmployeeLookupService hrLookup)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _hrLookup = hrLookup;
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

        var data = await _hrLookup.LookupAsync(empNo);
        if (!data.Success)
            return Json(new { error = data.Error ?? "لم يتم العثور على بيانات الموظف" });

        return Json(new
        {
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
            bloodGroup = data.BloodType
        });
        }
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


    [HttpGet]
    public IActionResult ViewUser()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ViewUser(string empFileNo)
    {
        if (string.IsNullOrEmpty(empFileNo))
        {
            TempData["ErrorMessage"] = "الرجاء إدخال رقم الملف.";
            return View();
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.FileNumber == empFileNo);

        if (user == null)
        {
            TempData["ErrorMessage"] = "المستخدم غير موجود.";
            return View();
        }

        var model = new ViewUserViewModel
        {
            Id = user.Id,
            EmpFileNo = user.FileNumber,
            EmpName = user.FullName,
            Department = user.Department,
            JobTypeName = user.JobtypeName,
            UserType = user.UserType
        };

        return View(model);
    }

    // حفظ نوع المستخدم فقط
    [HttpPost]
    public async Task<IActionResult> SaveUserType(ViewUserViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "المستخدم غير موجود.";
            return RedirectToAction("ViewUser");
        }

        user.UserType = model.UserType;
        user.Discriminator = model.UserType;
        await _userManager.UpdateAsync(user);

        TempData["SuccessMessage"] = "تم تحديث نوع المستخدم بنجاح!";
        return RedirectToAction("ViewUser");
    }

    // تصفير كلمة المرور
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "المستخدم غير موجود.";
            return RedirectToAction("ViewUser");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var newPassword = "Pms@" + user.FileNumber;
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            TempData["ResetPasswordMessage"] = $"تم تصفير كلمة المرور بنجاح (كلمة المرور الجديدة: {newPassword})";
        }
        else
        {
            TempData["ResetPasswordError"] = "فشل تصفير كلمة المرور.";
        }

        return RedirectToAction("ViewUser");
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ErrorMessage = "البيانات غير صالحة، يرجى التحقق.";
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            ViewBag.ErrorMessage = "المستخدم غير موجود.";
            return RedirectToAction("Login", "Account");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);

            ViewBag.SuccessMessage = "تم تغيير كلمة المرور بنجاح.";
            return View(model); 
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        ViewBag.ErrorMessage = string.Join(" | ", result.Errors.Select(e => e.Description));
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "المستخدم غير موجود.";
            return RedirectToAction("ViewUser");
        }
        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم حذف المستخدم بنجاح!";
        }
        else
        {
            TempData["ErrorMessage"] = "فشل حذف المستخدم.";
        }
        return RedirectToAction("ViewUser");
    }
    [HttpPost]
    public async Task<JsonResult> GetAllUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var userList = users.Select(u => new
        {
            u.Id,
            u.FileNumber,
            u.FullName,
            u.Department,
            u.JobtypeName,
            u.UserType
        }).ToList();
        return Json(userList);
    }
  
}




