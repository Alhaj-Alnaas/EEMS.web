using Microsoft.AspNetCore.Mvc;

namespace EEMS.web.Controllers
{
    public class UserManagementController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult UsersList()
        {
            //  قائمة المستخدمين  
            return View();
        }

        public IActionResult CreateUser()
        {
            return View();
        }

        public IActionResult ChangePassword(Guid id)
        {
            //  لتغيير كلمة مروره
            return View();
        }
    }
}
