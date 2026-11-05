namespace Core.Models
{
    /// <summary>مرحلة مسار تسجيل الموظف (جهاز ← مزامنة ← بيانات ← بوابات ← طباعة).</summary>
    public enum EmployeeEnrollmentPhase
    {
        /// <summary>موجود في النظام لكن بلا بصمة/كرت بعد.</summary>
        AwaitingCredential = 0,

        /// <summary>لديه بصمة أو كرت وينتظر إكمال بيانات HR/التسجيل.</summary>
        ReadyToRegister = 1,

        /// <summary>البيانات مكتملة وينتظر ربط البوابات.</summary>
        NeedsGates = 2,

        /// <summary>مسجّل ومربوط ببوابة واحدة على الأقل.</summary>
        Complete = 3
    }

    public sealed record EmployeeEnrollmentInfo(
        Guid EmployeeId,
        string FileNumber,
        string FullName,
        EmployeeEnrollmentPhase Phase,
        int TemplateCount,
        bool HasCard,
        int ActiveGateCount,
        bool HasBasicRegistration);

    public sealed record DeviceCredentialStatus(
        bool Found,
        Guid? EmployeeId,
        string? FileNumber,
        int TemplateCount,
        bool HasCard,
        string Message);
}
