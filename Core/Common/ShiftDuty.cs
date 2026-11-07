using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Duty-officer approval replaces the permit-type path and goes straight to execution.
    /// </summary>
    public static class ShiftDuty
    {
        public const string ResponsibilityCode = "43010";
        public const int JobCategoryId = 1;
        public const int DesignationId = 6;
        public const string StepLabel = "اعتماد مناوب";

        public static bool IsOfficer(User? user) =>
            user != null
            && string.Equals(user.JobStatus, "AE", StringComparison.OrdinalIgnoreCase)
            && user.JobCatId == JobCategoryId
            && user.DesignationId == DesignationId
            && ApprovalRoute.Same(user.ResponsibilityCode, ResponsibilityCode);

        public static bool IsPending(Permit permit) =>
            permit.ShiftApproval
            && permit.status == 'I'
            && string.Equals(permit.UnifiedStatus, nameof(UnifiedPermitStatus.PendingApproval), StringComparison.Ordinal);
    }
}
