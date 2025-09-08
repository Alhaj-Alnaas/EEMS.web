using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EEMS.web.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Advanced()
        {
            return View();
        }
    }
}
