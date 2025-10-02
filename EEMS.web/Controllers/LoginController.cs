using Core.Entities;
using Core.Interfaces.Services;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace EEMS.web.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IGates _gateService;

        public LoginController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IGates gateService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _gateService = gateService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CheckUser(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);

                if (user == null)
                {
                    return Json(new { success = false, message = "اسم المستخدم غير موجود." });
                }

                if (!string.Equals(user.JobStatus.Trim(), "AE", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "الحالة الوظيفية غير فعالة، لا يمكن الدخول." });
                }

                if (user.UserType == "SaftyUser")
                {
                    var gates = (await _gateService.GetAllAsync())
                                .Select(g => new { value = g.Id.ToString(), text = g.no })
                                .ToList();
                    return Json(new
                    {
                        success = true,
                        isSafty = true,
                        gates,
                        userName = user.FullName ?? user.UserName // استخدام FullName إذا موجود، وإلا UserName
                    });
                }
                return Json(new
                {
                    success = true,
                    isSafty = false,
                    userName = user.FullName ?? user.UserName // استخدام FullName إذا موجود، وإلا UserName
                });
            
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ في الخادم." });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                if (user == null)
                {
                    return Json(new { success = false, message = "اسم المستخدم غير موجود." });
                }

                if (!string.Equals(user.JobStatus.Trim(), "AE", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "الحالة الوظيفية غير فعالة، لا يمكن الدخول." });
                }

                // لو مستخدم SaftyUser يجب أن يختار بوابة
                if (user.UserType == "SaftyUser" && string.IsNullOrEmpty(model.SelectedGate))
                {
                    return Json(new { success = false, message = "يرجى اختيار البوابة." });
                }

                
                if ( string.IsNullOrEmpty(model.Password))
                {
                    return Json(new { success = false, message = "يرجى إدخال كلمة المرور." });
                }

                var result = await _signInManager.PasswordSignInAsync(
                              model.Username,
                              model.Password,
                              isPersistent: false,
                              lockoutOnFailure: false
                 );

                if (result.Succeeded)
                {

                    var userData = new
                    {
                        id = user.Id,
                        userName = user.UserName,
                        fullName = user.FullName,
                        userType = user.UserType,
                        jobStatus = user.JobStatus,
                        job = user.JobtypeName,
                        responsibilityCode = user.ResponsibilityCode,
                        jobCatId = user.JobCatId
                    };

                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    HttpContext.Session.SetString("UserData", JsonSerializer.Serialize(userData, options));
                    
                    HttpContext.Session.SetString("ResponsibilityCode", user.ResponsibilityCode);
                    HttpContext.Session.SetString("UserName", user.UserName);
                    HttpContext.Session.SetString("FullName", user.FullName);
                    HttpContext.Session.SetString("UserType", user.UserType);
                    HttpContext.Session.SetString("JobStatus", user.JobStatus);
                    HttpContext.Session.SetString("JobtypeName", user.JobtypeName);
                    HttpContext.Session.SetString("JobCatId", user.JobCatId.ToString());


                    return Json(new { success = true, redirectUrl = Url.Action("PermitIndex", "Permit") });
                }

                return Json(new { success = false, message = "اسم المستخدم أو كلمة المرور غير صحيحة." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ في الخادم." });
            }
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetUserData()
        {
            try
            {
                var userDataJson = HttpContext.Session.GetString("UserData");
                if (string.IsNullOrEmpty(userDataJson))
                    return NotFound(new { message = "لم يتم العثور على بيانات المستخدم" });

                var userData = JsonSerializer.Deserialize<dynamic>(userDataJson);
                return Ok(userData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ في الخادم" });
            }
        }

      
        // ---------------- Logout ----------------
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Login");
        }

        // ---------------- Access Denied ----------------
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

