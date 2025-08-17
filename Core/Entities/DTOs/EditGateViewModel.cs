using Core.Entities;
using System;
using System.Collections.Generic;
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

    // قائمة كل أنواع التصاريح المتاحة للعرض
    public List<PermitType> PermitTypes { get; set; } = new List<PermitType>();

    // قائمة الـ IDs المحددة من قبل المستخدم (Many-to-Many)
    [Required(ErrorMessage = "يجب اختيار نوع واحد على الأقل من التصاريح")]
    [Display(Name = "أنواع التصاريح المسموح بها")]
    public List<int> SelectedPermitTypeIds { get; set; } = new List<int>();
}
