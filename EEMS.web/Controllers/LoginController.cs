using Core.Entities;
using Core.Interfaces.Services;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace EEMS.web.Controllers
{
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
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
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
                    return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
                }

                return Json(new { success = false, message = "اسم المستخدم أو كلمة المرور غير صحيحة." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ في الخادم." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Login");
        }
    }
}




//using Core.Entities;
//using Core.Interfaces.Services;
//using EEMS.web.ViewModels;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Identity.UI.V4.Pages.Account.Internal;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;


//namespace EEMS.web.Controllers
//{
//    public class LoginController : Controller
//    {
//        private readonly UserManager<User> _userManager;
//        private readonly SignInManager<User> _signInManager;
//        private readonly IGates _gateService;

//        public LoginController(
//            UserManager<User> userManager,
//            SignInManager<User> signInManager,
//            IGates gateService)
//        {
//            _userManager = userManager;
//            _signInManager = signInManager;
//            _gateService = gateService;
//        }

//        [HttpGet]
//        public IActionResult Login()
//        {
//            return View(new LoginViewModel());
//        }


//        [HttpPost]
//        public async Task<IActionResult> CheckUser(string username)
//        {
//            var user = await _userManager.FindByNameAsync(username);

//            if (user == null)
//            {
//                return Json(new { success = false, message = "اسم المستخدم غير موجود." });
//            }


//            if (!string.Equals(user.JobStatus.Trim(), "AE", StringComparison.OrdinalIgnoreCase))
//            {
//                return Json(new { success = false, message = "الحالة الوظيفية غير فعالة، لا يمكن الدخول." });
//             }


//            if (user.UserType == "SaftyUser")
//            {
//                var gates = (await _gateService.GetAllAsync())
//                            .Select(g => new { value = g.Id.ToString(), text = g.no })
//                            .ToList();

//                return Json(new { success = true, isSafty = true, gates });
//            }

//            return Json(new { success = true, isSafty = false });
//        }

//        // 
//        [HttpPost]
//        public async Task<IActionResult> Login(LoginViewModel model)
//        {
//            var user = await _userManager.FindByNameAsync(model.Username);

//            if (user == null)
//            {
//                return Json(new { success = false, message = "اسم المستخدم غير موجود." });
//            }


//            if (!string.Equals(user.JobStatus.Trim(), "AE", StringComparison.OrdinalIgnoreCase))
//            {
//                return Json(new { success = false, message = "الحالة الوظيفية غير فعالة، لا يمكن الدخول." });
//            }

//            // لو مستخدم SaftyUser يجب أن يختار بوابة
//    if (user.UserType == "SaftyUser" && string.IsNullOrEmpty(model.SelectedGate))
//    {
//        return Json(new { success = false, message = "يرجى اختيار البوابة." });
//    }

//            var result = await _signInManager.PasswordSignInAsync(
//                          model.Username,
//                          model.Password,
//                          isPersistent: false,
//                          lockoutOnFailure: false
//             );

//            if (result.Succeeded)
//            {
//                return RedirectToAction("Index", "Home"); // ✅ الصفحة الرئيسية بعد الدخول
//            }

//            model.ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة.";
//            return View(model);
//        }



//        [HttpPost]
//        public async Task<IActionResult> Logout()
//        {
//            await _signInManager.SignOutAsync();
//            return RedirectToAction("Login", "Login");
//        }
//    }
//}
