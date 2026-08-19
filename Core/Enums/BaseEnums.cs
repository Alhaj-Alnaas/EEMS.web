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

        // ============================================================
        // Unified Permit Management System (Phase 1 + Phase 2) additions
        // ============================================================

        /// <summary>
        /// High level classification of a unified permit, spans both
        /// Phase 1 (employee access) and Phase 2 (materials/visitors/cars).
        /// </summary>
        public enum PermitClassification
        {
            [Description("مواد")]
            Materials,

            [Description("زوار")]
            Visitors,

            [Description("سيارات")]
            Cars,

            [Description("دخول موظفين")]
            EmployeeAccess
        }

        /// <summary>
        /// Unified lifecycle status for a permit. Legacy <see cref="Core.Entities.Permit.status"/>
        /// (char: I/A/J/R/C/D) is mapped conceptually via <see cref="Core.Common.StatusMapper"/>.
        /// </summary>
        public enum UnifiedPermitStatus
        {
            [Description("مسودة")]
            Draft,

            [Description("قيد الاعتماد")]
            PendingApproval,

            [Description("معتمد")]
            Approved,

            [Description("نشط")]
            Active,

            [Description("مرفوض")]
            Rejected,

            [Description("منتهي")]
            Expired,

            [Description("ملغي")]
            Cancelled
        }

        /// <summary>Which phase of PMS a feature/permit belongs to.</summary>
        public enum PermitPhase
        {
            [Description("المرحلة الأولى - الدخول البيومتري")]
            Phase1,

            [Description("المرحلة الثانية - تصاريح المعدات والزوار والسيارات")]
            Phase2
        }

        /// <summary>Direction of a movement/access event at a gate.</summary>
        public enum MovementDirection
        {
            [Description("دخول")]
            In,

            [Description("خروج")]
            Out
        }

        /// <summary>Runtime connectivity status of a biometric reader (DDD Readers.Status).</summary>
        public enum ReaderStatus
        {
            [Description("غير متصل")]
            Offline,

            [Description("متصل")]
            Online,

            [Description("معطل / خطأ")]
            Faulty
        }

        /// <summary>Command types queued to a biometric reader (DDD DeviceCommands.CommandType).</summary>
        public enum DeviceCommandType
        {
            [Description("إضافة بصمة")]
            AddTemplate,

            [Description("حذف بصمة")]
            RemoveTemplate,

            [Description("إضافة صلاحية")]
            AddPermission,

            [Description("حذف صلاحية")]
            RemovePermission,

            [Description("مزامنة")]
            Sync,

            [Description("طلب سجل حضور")]
            QueryAttLog,

            [Description("طلب مستخدمي القارئة")]
            QueryUserInfo,

            [Description("طلب قوالب البصمات")]
            QueryFingerTmp,

            [Description("طلب بصمات (BIODATA)")]
            QueryBioData,

            [Description("طلب إحصائيات الجهاز")]
            QueryDeviceInfo,

            [Description("تحديث مستخدم على القارئة")]
            UpdateUserInfo,

            [Description("مزامنة كاملة للبيانات")]
            SyncAllData,

            [Description("اختبار اتصال")]
            TestConnection,

            [Description("إعادة تشغيل")]
            Reboot,

            [Description("مزامنة شاملة (إحصائيات + حركة + موظفين + بصمات)")]
            FullSync
        }

        /// <summary>Status of a command dispatched to a biometric reader/device.</summary>
        public enum DeviceCommandStatus
        {
            [Description("قيد الانتظار")]
            Pending,

            [Description("تم الإرسال")]
            Sent,

            [Description("تم الاستلام")]
            Acknowledged,

            [Description("فشل")]
            Failed
        }

        /// <summary>Type of a step inside a configurable approval workflow.</summary>
        public enum WorkflowStepType
        {
            [Description("اعتماد الإدارة الطالبة")]
            DepartmentApproval,

            [Description("اعتماد القسم الأمني")]
            SecurityApproval,

            [Description("اعتماد قسم التصاريح")]
            PermitsSectionApproval,

            [Description("خطوة مخصصة")]
            Custom
        }

        /// <summary>Biometric template kind captured for an employee.</summary>
        public enum BiometricTemplateType
        {
            [Description("بصمة الإصبع")]
            Fingerprint,

            [Description("بصمة الوجه")]
            Face
        }

        /// <summary>Source that generated a movement/access log entry.</summary>
        public enum MovementSource
        {
            [Description("بيومتري")]
            Biometric,

            [Description("يدوي")]
            Manual,

            [Description("ضابط البوابة")]
            GateOfficer
        }

        /// <summary>Decision taken on a workflow approval step.</summary>
        public enum ApprovalDecision
        {
            [Description("معتمد")]
            Approved,

            [Description("مرفوض")]
            Rejected,

            [Description("مرجع")]
            Returned
        }

    }
}
