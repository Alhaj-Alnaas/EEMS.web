using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace PMS.web.Controllers
{
    /// <summary>
    /// Signs out on the HTTP request itself. A Blazor circuit cannot delete the
    /// authentication cookie, so logout must be a full browser navigation.
    /// </summary>
    [AllowAnonymous]
    public class LogoutController : Controller
    {
        private readonly SignInManager<User> _signInManager;

        public LogoutController(SignInManager<User> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpGet("/logout")]
        public async Task<IActionResult> Index()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return Redirect("/login");
        }
    }
}
