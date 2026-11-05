using System.ComponentModel.DataAnnotations;

namespace Core.Models.Security
{
    public class AccessLevelEditModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "اسم المستوى مطلوب")]
        [MaxLength(150)]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsGateOfficer { get; set; }
        public bool ReceivesDeviceAlerts { get; set; }

        public List<Guid> SelectedTaskIds { get; set; } = new();
    }

    public class CreateUserAccountModel
    {
        [Required(ErrorMessage = "الرقم الوظيفي مطلوب")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "يجب أن يحتوي الرقم الوظيفي على أرقام فقط")]
        public string EmpFileNo { get; set; } = "";

        public string EmpName { get; set; } = "";
        public string Department { get; set; } = "";
        public string EmpPhone { get; set; } = "";
        public string EmpEmail { get; set; } = "";
        public int? RespCodeId { get; set; }
        public string EmpResponsibilityCode { get; set; } = "";
        public int? JobCatId { get; set; }
        public string JobStatus { get; set; } = "";
        public int? DesignationId { get; set; }
        public string JobTypeName { get; set; } = "";

        [Required(ErrorMessage = "يجب اختيار مستوى الدخول")]
        public Guid? AccessLevelId { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن لا تقل عن {2} أحرف", MinimumLength = 6)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير ورقم واحد على الأقل.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(Password), ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class UserAccountListItem
    {
        public string Id { get; set; } = "";
        public string FileNumber { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Department { get; set; } = "";
        public string JobTypeName { get; set; } = "";
        public string JobStatus { get; set; } = "";
        public string? AccessLevelName { get; set; }
    }

    public class UserAccountDetail
    {
        public string Id { get; set; } = "";
        public string EmpFileNo { get; set; } = "";
        public string EmpName { get; set; } = "";
        public string Department { get; set; } = "";
        public string JobTypeName { get; set; } = "";
        public string EmpEmail { get; set; } = "";
        public string JobStatus { get; set; } = "";
        public Guid? AccessLevelId { get; set; }
        public string? AccessLevelName { get; set; }
    }
}
