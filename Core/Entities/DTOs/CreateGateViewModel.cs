using Core.Entities;
using System.ComponentModel.DataAnnotations;

namespace EEMS.web.ViewModels
{
    public class CreateGateViewModel
    {
        [Required(ErrorMessage = "يجب اختيار نوع تصريح واحد على الأقل")]
        public List<int> SelectedPermitTypeIds { get; set; } = new();

        public List<PermitType> PermitTypes { get; set; } = new();

        [Required(ErrorMessage = " يجب إدخال رقم البوابة ")]
        public string No { get; set; }
        [Required(ErrorMessage = "يجب إدخال وصف للبوابة ")]
        public string Description { get; set; }

        public bool IsActive { get; set; }

        public string Remarks { get; set; }
    }
}
