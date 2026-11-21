using System.Globalization;
using System.Text;

namespace Core.Common
{
    public static class LaborCompanyState
    {
        public const string Active = "Active";
        public const string Stopped = "Stopped";

        public static string Label(string? status) =>
            status == Stopped ? "متوقفة" : "مستمرة";

        public static bool TryParse(string? text, out string status)
        {
            var key = LaborText.MatchKey(text);
            status = key switch
            {
                "مستمره" or "مستمر" or "نشطه" or "نشط" or "ACTIVE" => Active,
                "متوقفه" or "موقوفه" or "متوقف" or "STOPPED" => Stopped,
                _ => ""
            };
            return status.Length > 0;
        }
    }

    public static class LaborWorkerState
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string Ended = "Ended";

        public static string Label(string? status) => status switch
        {
            Suspended => "موقوف مؤقتا",
            Ended => "إنهاء خدمة",
            _ => "مستمر"
        };

        public static string Badge(string? status) => status switch
        {
            Suspended => "status-pending",
            Ended => "status-closed",
            _ => "status-approved"
        };

        public static bool TryParse(string? text, out string status)
        {
            var key = LaborText.MatchKey(text);
            if (key.Length == 0)
            {
                status = Active;
                return true;
            }

            status = key switch
            {
                "مستمر" or "مستمره" or "نشط" or "ACTIVE" => Active,
                "موقوف مؤقتا" or "موقوف" or "ايقاف مؤقت" or "SUSPENDED" => Suspended,
                "انهاء خدمه" or "انهاء الخدمه" or "منهي الخدمه" or "منتهيه الخدمه" or "ENDED" => Ended,
                _ => ""
            };
            return status.Length > 0;
        }
    }

    public static class LaborBatchMode
    {
        public const string Add = "Add";
        public const string Match = "Match";

        public static string Label(string? mode) =>
            mode == Match ? "مطابقة" : "إضافة";
    }

    public static class LaborBatchState
    {
        public const string Draft = "Draft";
        public const string Applied = "Applied";
        public const string Cancelled = "Cancelled";

        public static string Label(string? state) => state switch
        {
            Applied => "مُعتمدة",
            Cancelled => "ملغاة",
            _ => "بانتظار الاعتماد"
        };
    }

    public static class LaborLineAction
    {
        public const string Add = "Add";
        public const string UpdateStatus = "UpdateStatus";
        public const string CloseService = "CloseService";
        public const string Renumber = "Renumber";
        public const string Skip = "Skip";

        public static string Label(string? action) => action switch
        {
            Add => "إضافة",
            UpdateStatus => "تعديل الحالة",
            CloseService => "إغلاق خدمة",
            Renumber => "إعادة ترقيم",
            Skip => "موجود مسبقاً",
            _ => action ?? ""
        };

        public static string Badge(string? action) => action switch
        {
            Add => "status-approved",
            UpdateStatus => "status-pending",
            CloseService => "status-closed",
            Renumber => "status-active",
            _ => "status-draft"
        };
    }

    public static class LaborSheet
    {
        public static readonly string[] Headers =
        [
            "اسم العامل",
            "الجنسية",
            "رقم الجواز",
            "الحالة",
            "العنوان",
            "ملاحظات"
        ];
    }

    /// <summary>Permit number: YY + company code (2) + sequence (3) = 7 digits.</summary>
    public static class LaborPermitNumber
    {
        public const int Length = 7;

        public static string Compose(int year, string companyCode, int sequence)
        {
            if (sequence is < 1 or > 999)
                throw new InvalidOperationException("تسلسل التصريح يجب أن يكون بين 001 و 999.");

            var code = (companyCode ?? "").Trim();
            if (code.Length != 2 || code.Any(ch => !char.IsDigit(ch)))
                throw new InvalidOperationException("رمز الشركة في رقم التصريح يجب أن يكون خانتين.");

            return string.Create(Length, (year, code, sequence), static (span, value) =>
            {
                var yy = (value.year % 100).ToString("00", CultureInfo.InvariantCulture);
                var seq = value.sequence.ToString("000", CultureInfo.InvariantCulture);
                yy.AsSpan().CopyTo(span);
                value.code.AsSpan().CopyTo(span[2..]);
                seq.AsSpan().CopyTo(span[4..]);
            });
        }

        /// <summary>Text encoded in the card QR. A gate reader can store and match this value later.</summary>
        public const string QrPrefix = "PMS-LABOR:";

        public static string QrText(string? permitNo) => QrPrefix + (permitNo ?? "").Trim();

        public static bool TryReadSequence(string? permitNo, int year, string companyCode, out int sequence)
        {
            sequence = 0;
            if (string.IsNullOrWhiteSpace(permitNo) || permitNo.Length != Length)
                return false;

            var prefix = string.Create(4, (year, companyCode), static (span, value) =>
            {
                var yy = (value.year % 100).ToString("00", CultureInfo.InvariantCulture);
                yy.AsSpan().CopyTo(span);
                value.companyCode.AsSpan().CopyTo(span[2..]);
            });

            if (!permitNo.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(permitNo.AsSpan(4, 3), NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
                && sequence is >= 1 and <= 999;
        }
    }

    public static class LaborText
    {
        public static string Compact(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var parts = value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts);
        }

        public static string PassportKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var buffer = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch) || ch is '-' or '_')
                    continue;
                buffer.Append(char.ToUpperInvariant(ch));
            }

            return buffer.ToString();
        }

        public static string MatchKey(string? value)
        {
            var text = StripMarks(Compact(value));
            if (text.Length == 0)
                return "";

            var buffer = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                buffer.Append(ch switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                    'ة' => 'ه',
                    'ى' => 'ي',
                    _ => ch
                });
            }

            return buffer.ToString().ToUpperInvariant();
        }

        private static string StripMarks(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    buffer.Append(ch);
            }

            return buffer.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
