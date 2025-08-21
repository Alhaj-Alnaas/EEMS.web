using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EEMS.web.ViewModels
{
    public class CreateUserViewModel
    {
        // البيانات الأساسية
        [Required(ErrorMessage = "الرقم الوظيفي مطلوب")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "يجب أن يحتوي الرقم الوظيفي على أرقام فقط")]
        [Display(Name = "الرقم الوظيفي")]
        public string EmpFileNo { get; set; }

        [Display(Name = "الاسم الكامل")]
        public string EmpName { get; set; }

        [Display(Name = "الإدارة")]
        public string Department { get; set; }
        public string EmpPhone { get; set; }          
        [Display(Name = "البريد الإلكتروني")]
        public string EmpEmail { get; set; }          
        public int? RespCodeId { get; set; }         
        public string EmpResponsibilityCode { get; set; } 
        public string PerRespCodeNoName { get; set; } 
        public int? JobCatId { get; set; }    
        [Display(Name = "الحالة الوظيفية")]
        public string JobStatus { get; set; }         
        public int? DesignationId { get; set; }       

        [Display(Name = "نوع الوظيفة")]
        public string JobTypeName { get; set; }       

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن لا تقل عن {2} أحرف", MinimumLength = 6)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير ورقم واحد على الأقل.")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; }

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
        [Display(Name = "تأكيد كلمة المرور")]
        public string ConfirmPassword { get; set; }

        // تصنيف المستخدم
        [Required(ErrorMessage = "يجب اختيار تصنيف المستخدم")]
        [Display(Name = "تصنيف المستخدم")]
        public string UserType { get; set; }
    }

}
