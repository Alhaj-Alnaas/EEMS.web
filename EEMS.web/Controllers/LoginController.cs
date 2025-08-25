using Core.Entities;
using Core.Interfaces.Services;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            //if (!ModelState.IsValid)
            //    return View(model);

            var user = await _userManager.FindByNameAsync(model.Username);

            if (user == null)
            {
                model.ErrorMessage = "اسم المستخدم غير موجود.";
                return View(model);
            }

            // التحقق من الحالة الوظيفية
            if (!string.Equals(user.JobStatus.Trim(), "AE", StringComparison.OrdinalIgnoreCase))
            {
                model.ErrorMessage = "الحالة الوظيفية غير فعالة، لا يمكن الدخول.";
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
              model.Username,
              model.Password,
              isPersistent: false,
              lockoutOnFailure: false
 );

            if (!result.Succeeded)
            {
                model.ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة.";
                return View(model);
            }

            if (user.UserType == "SaftyUser")
            {
                var gates = await _gateService.GetAllAsync();
                model.Gates = gates.Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.no }).ToList();
                model.IsSaftyUser = true;

                if (!model.SelectedGateId.HasValue)
                {
                    model.ErrorMessage = "يرجى اختيار البوابة.";
                    return View(model);
                }
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
