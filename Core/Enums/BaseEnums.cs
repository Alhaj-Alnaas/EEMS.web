using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Enums
{
    public class BaseEnums
    {
        public enum UnitType
        {
            قطعة,
            كيلوجرام,
            جرام,
            طن,
            لتر,
            متر,
            متر_مربع,
            متر_مكعب,
            صندوق,
            عبوة,
            كرتونة,
            شحنة,
            طرد,
            غير_معروف
        }

        public enum Nationality
        {
            ليبي,
            مصري,
            سوداني,
            تونسي,
            جزائري,
            مغربي,
            فلسطيني,
            يمني,
            سوري,
            عراقي,
            هندي,
            باكستاني,
            بنغلاديشي,
            فلبيني,
            إندونيسي,
            ماليزي,
            تركي,
            صيني,
            ياباني,
            كوري,
            غير_معروف
        }

        public enum EnumPermitType
        {
            [Description("إستخراج")]
            Export,

            [Description("إدخال")]
            Import,

            [Description("إستخراج وإدخال")]
            ExtractAndInsert
        }

        public enum ProcedureType
        {
            [Description("إدخال")]
            Insert,

            [Description("تعديل")]
            Update,

            [Description("حدف")]
            Delete,
                [Description("إغلاق")]
            Close,

            [Description("اعتماد")]
            Approve,

            [Description("ترجيع")]
            Returen
        }

    }
}
