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
    }
}
