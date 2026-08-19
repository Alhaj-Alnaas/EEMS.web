using System.ComponentModel.DataAnnotations;

namespace Core.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "الرجاء إدخال رقم الملف")]
        [Display(Name = "رقم الملف")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "الرجاء إدخال كلمة المرور")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = "";

        [Display(Name = "البوابة")]
        public string? SelectedGate { get; set; }

        public List<GateOption> Gates { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }

    /// <summary>Lightweight select-list style option, avoids an MVC dependency in Core.</summary>
    public class GateOption
    {
        public string Value { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
