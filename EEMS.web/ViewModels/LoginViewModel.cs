using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EEMS.web.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        public string Username { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool IsSaftyUser { get; set; } = false;

        public List<SelectListItem> Gates { get; set; }

        public int? SelectedGateId { get; set; }

        public string ErrorMessage { get; set; }
    }
}
