using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Maps between the legacy single-character permit status
    /// (I = قيد الاعتماد, A = معتمد, J = مرفوض, R = مرجع, C = مغلق, D = محذوف)
    /// and the new <see cref="UnifiedPermitStatus"/> enum used across the
    /// unified Permit Management System.
    /// </summary>
    public static class StatusMapper
    {
        public static UnifiedPermitStatus ToUnified(char legacyStatus)
        {
            return legacyStatus switch
            {
                'I' => UnifiedPermitStatus.PendingApproval,
                'A' => UnifiedPermitStatus.Approved,
                'J' => UnifiedPermitStatus.Rejected,
                'R' => UnifiedPermitStatus.PendingApproval, // "مرجع" (returned) is still awaiting the requester's action
                'C' => UnifiedPermitStatus.Active,          // "مغلق/تم التنفيذ" -> considered fulfilled/active-history
                'D' => UnifiedPermitStatus.Cancelled,
                _ => UnifiedPermitStatus.Draft,
            };
        }

        public static char ToLegacy(UnifiedPermitStatus unifiedStatus)
        {
            return unifiedStatus switch
            {
                UnifiedPermitStatus.Draft => 'I',
                UnifiedPermitStatus.PendingApproval => 'I',
                UnifiedPermitStatus.Approved => 'A',
                UnifiedPermitStatus.Active => 'A',
                UnifiedPermitStatus.Rejected => 'J',
                UnifiedPermitStatus.Expired => 'C',
                UnifiedPermitStatus.Cancelled => 'D',
                _ => 'I',
            };
        }

        /// <summary>Human readable Arabic description for the legacy char status.</summary>
        public static string LegacyStatusDescription(char legacyStatus) => legacyStatus switch
        {
            'I' => "قيد الاعتماد",
            'A' => "معتمد",
            'J' => "مرفوض",
            'R' => "مرجع",
            'C' => "مغلق / تم التنفيذ",
            'D' => "محذوف",
            _ => "غير معروف",
        };

        /// <summary>Human readable Arabic description for the unified status.</summary>
        public static string UnifiedStatusDescription(UnifiedPermitStatus status) => status switch
        {
            UnifiedPermitStatus.Draft => "مسودة",
            UnifiedPermitStatus.PendingApproval => "قيد الاعتماد",
            UnifiedPermitStatus.Approved => "معتمد",
            UnifiedPermitStatus.Active => "نشط",
            UnifiedPermitStatus.Rejected => "مرفوض",
            UnifiedPermitStatus.Expired => "منتهي",
            UnifiedPermitStatus.Cancelled => "ملغي",
            _ => "غير معروف",
        };

        /// <summary>Status shown on the responsibility-center permit list.</summary>
        public static (string Css, string Label) ListStatus(Permit permit)
        {
            if (permit.status == 'D'
                || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Cancelled))
                return ("status-cancelled", "ملغي");

            if (permit.isClosed || permit.status == 'C')
            {
                if (permit.UnifiedStatus == nameof(UnifiedPermitStatus.Expired)
                    || permit.statusDescription == "مغلق")
                    return ("status-closed", "مغلق");
                return ("status-closed", "منفذ");
            }

            if (permit.status == 'R')
                return ("status-returned", "مرجع");

            if (IsDraft(permit))
                return ("status-draft", "مسودة");

            if (permit.status == 'J' || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Rejected))
                return ("status-rejected", "مرفوض");

            if (permit.status == 'A'
                || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Approved)
                || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Active))
                return ("status-approved", "تحت التنفيذ");

            return ("status-pending", "تحت الاعتماد");
        }

        public static bool IsDraft(Permit permit) =>
            string.Equals(permit.UnifiedStatus, nameof(UnifiedPermitStatus.Draft), StringComparison.Ordinal);

        public static bool IsApprovedOrExecuted(Permit permit)
        {
            if (permit.isClosed || permit.status is 'A' or 'C')
                return true;

            return permit.UnifiedStatus is nameof(UnifiedPermitStatus.Approved)
                or nameof(UnifiedPermitStatus.Active)
                or nameof(UnifiedPermitStatus.Expired);
        }
    }
}
