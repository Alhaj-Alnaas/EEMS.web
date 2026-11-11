using System;
using System.ComponentModel.DataAnnotations;

public class EditGateViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "رقم البوابة مطلوب")]
    [Display(Name = "رقم البوابة")]
    public string No { get; set; }

    [Required(ErrorMessage = "الوصف مطلوب")]
    [Display(Name = "الوصف")]
    public string Description { get; set; }

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; }

    [Display(Name = "ملاحظات")]
    public string Remarks { get; set; }
}
