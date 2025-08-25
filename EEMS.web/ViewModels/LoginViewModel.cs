using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EEMS.web.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "الرجاء إدخال رقم الملف")]
        [Display(Name = "رقم الملف")]
        public string Username { get; set; }

        [Required(ErrorMessage = "الرجاء إدخال كلمة المرور")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; }

        [Display(Name = "البوابة")]
        public string SelectedGate { get; set; }
        public List<SelectListItem> Gates { get; set; } = new();

        public string ErrorMessage { get; set; }
    }
}
