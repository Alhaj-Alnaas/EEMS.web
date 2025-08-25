using System.ComponentModel.DataAnnotations;

namespace EEMS.web.ViewModels
{
    public class ViewUserViewModel
    {
        public string Id { get; set; }

        [Display(Name = "الرقم الوظيفي")]
        public string EmpFileNo { get; set; }

        [Display(Name = "اسم الموظف")]
        public string EmpName { get; set; }

        [Display(Name = "الإدارة")]
        public string Department { get; set; }

        [Display(Name = "الوظيفة")]
        public string JobTypeName { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        public string EmpEmail { get; set; }

        [Display(Name = "الحالة الوظيفية")]
        public string JobStatus { get; set; }

        [Display(Name = "نوع المستخدم")]
        public string UserType { get; set; }
    }
}
