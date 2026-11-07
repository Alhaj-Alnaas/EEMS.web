using Core.Entities;
using Core.Models;
using static Core.Enums.BaseEnums;

namespace Services.Permits
{
    /// <summary>
    /// Decides the next execution action from the permit type and the movements already recorded.
    /// </summary>
    public static class PermitExecutionPlan
    {
        public static PermitExecutionStep Next(Permit permit, IReadOnlyList<MovementLog> movements)
        {
            var ins = movements.Count(m => m.Direction == MovementDirection.In);
            var outs = movements.Count(m => m.Direction == MovementDirection.Out);
            var classification = permit.classification?.Trim() ?? "";

            if (IsVisitors(classification))
                return Visitor(ins, outs);

            if (IsCars(classification))
                return Vehicle(permit, ins, outs);

            return Materials(permit, ins, outs);
        }

        public static bool IsOverdue(Permit permit, IReadOnlyList<MovementLog> movements, DateTime now, out DateTime since, out string waiting)
        {
            since = default;
            waiting = "";
            var step = Next(permit, movements);
            if (step.Completed || permit.isClosed || permit.status is 'C' or 'D')
                return false;

            var classification = permit.classification?.Trim() ?? "";
            if (IsVisitors(classification) && step.Direction == MovementDirection.Out)
            {
                var entry = movements.Where(m => m.Direction == MovementDirection.In).OrderBy(m => m.EventTime).FirstOrDefault();
                if (entry == null || now - entry.EventTime < TimeSpan.FromHours(24))
                    return false;
                since = entry.EventTime;
                waiting = "تسجيل ساعة الخروج";
                return true;
            }

            if (IsCars(classification) && permit.VehicleDetail?.InternalUse != false && step.Direction == MovementDirection.In)
            {
                var exit = movements.Where(m => m.Direction == MovementDirection.Out).OrderByDescending(m => m.EventTime).FirstOrDefault();
                if (exit == null || now - exit.EventTime < TimeSpan.FromHours(24))
                    return false;
                since = exit.EventTime;
                waiting = "تسجيل ساعة الدخول";
                return true;
            }

            return false;
        }

        /// <summary>
        /// A visitor entered without leaving, or an internal vehicle left without returning.
        /// After the validity date these stay open for the gate officer.
        /// </summary>
        public static bool ShouldKeepOpenAfterValidity(Permit permit, IReadOnlyList<MovementLog> movements)
        {
            if (permit.isClosed || permit.status is 'C' or 'D')
                return false;
            if (IsMaterials(permit.classification))
                return false;
            return HasOpenMovement(permit, movements);
        }

        /// <summary>
        /// Extract-and-insert materials recorded an entry and not the exit.
        /// Import-only and export-only finish on their single movement.
        /// </summary>
        public static bool MaterialsEntryWithoutExit(Permit permit, IReadOnlyList<MovementLog> movements)
        {
            if (!IsMaterials(permit.classification))
                return false;
            if (!string.Equals(permit.type, nameof(EnumPermitType.ExtractAndInsert), StringComparison.OrdinalIgnoreCase))
                return false;

            var ins = movements.Count(m => m.Direction == MovementDirection.In);
            var outs = movements.Count(m => m.Direction == MovementDirection.Out);
            return ins > 0 && outs < ins;
        }

        /// <summary>Gate that recorded the movement still waiting to be closed.</summary>
        public static Guid? OpenMovementGate(Permit permit, IReadOnlyList<MovementLog> movements)
        {
            if (!HasOpenMovement(permit, movements))
                return null;

            var classification = permit.classification?.Trim() ?? "";
            if (IsVisitors(classification))
            {
                var entry = movements.Where(m => m.Direction == MovementDirection.In).OrderByDescending(m => m.EventTime).FirstOrDefault();
                return entry?.GateId is Guid gate && gate != Guid.Empty ? gate : null;
            }

            var exit = movements.Where(m => m.Direction == MovementDirection.Out).OrderByDescending(m => m.EventTime).FirstOrDefault();
            return exit?.GateId is Guid exitGate && exitGate != Guid.Empty ? exitGate : null;
        }

        public static bool HasOpenMovement(Permit permit, IReadOnlyList<MovementLog> movements)
        {
            var classification = permit.classification?.Trim() ?? "";
            var ins = movements.Count(m => m.Direction == MovementDirection.In);
            var outs = movements.Count(m => m.Direction == MovementDirection.Out);
            if (IsVisitors(classification))
                return ins > 0 && outs < ins;
            if (IsCars(classification) && permit.VehicleDetail?.InternalUse != false)
                return outs > ins;
            return false;
        }

        private static PermitExecutionStep Visitor(int ins, int outs)
        {
            if (ins == 0)
            {
                return Open(MovementDirection.In, "تسجيل ساعة الدخول",
                    "يبقى التصريح مفتوحاً حتى تسجيل ساعة الخروج.",
                    "بانتظار تسجيل الدخول");
            }

            if (outs == 0)
            {
                return Close(MovementDirection.Out, "تسجيل ساعة الخروج",
                    "تسجيل الخروج يقفل التصريح.",
                    "بانتظار تسجيل الخروج");
            }

            return Done("اكتمل تسجيل الدخول والخروج");
        }

        private static PermitExecutionStep Vehicle(Permit permit, int ins, int outs)
        {
            if (permit.VehicleDetail?.InternalUse == false)
            {
                return outs == 0
                    ? Close(MovementDirection.Out, "تنفيذ التصريح", "يُقفل التصريح بعد التنفيذ.", "بانتظار التنفيذ")
                    : Done("تم التنفيذ");
            }

            var trips = Math.Max(1, permit.VehicleDetail?.TripCount ?? 1);
            if (outs == ins && outs < trips)
            {
                return Open(MovementDirection.Out, "تسجيل ساعة خروج المركبة",
                    $"الرحلة {outs + 1} من {trips}. تبقى مفتوحة حتى تسجيل العودة.",
                    $"بانتظار خروج الرحلة {outs + 1} من {trips}");
            }

            if (outs == ins + 1 && ins < trips)
            {
                var last = ins + 1 >= trips;
                return last
                    ? Close(MovementDirection.In, "تسجيل ساعة دخول المركبة",
                        $"الرحلة {ins + 1} من {trips}. تسجيل الدخول يقفل التصريح.",
                        $"بانتظار دخول الرحلة {ins + 1} من {trips}")
                    : Open(MovementDirection.In, "تسجيل ساعة دخول المركبة",
                        $"الرحلة {ins + 1} من {trips}. بعد الدخول يمكن بدء الرحلة التالية.",
                        $"بانتظار دخول الرحلة {ins + 1} من {trips}");
            }

            return Done($"اكتملت الرحلات ({trips})");
        }

        private static PermitExecutionStep Materials(Permit permit, int ins, int outs)
        {
            if (string.Equals(permit.type, nameof(EnumPermitType.ExtractAndInsert), StringComparison.OrdinalIgnoreCase))
            {
                if (ins == 0)
                {
                    return Open(MovementDirection.In, "تنفيذ الإدخال",
                        "يبقى الإخراج مفتوحاً حتى يُقفل يدوياً. إذا انتهى السريان قبل تسجيل الخروج يُغلق التصريح، وتُعد الجهة الطالبة تصريح استخراج.",
                        "بانتظار تنفيذ الإدخال");
                }

                if (outs == 0)
                {
                    return Close(MovementDirection.Out, "إغلاق الإخراج",
                        "إغلاق الإخراج يقفل التصريح.",
                        "الإخراج مفتوح");
                }

                return Done("اكتمل الإدخال والإخراج");
            }

            if (string.Equals(permit.type, nameof(EnumPermitType.Export), StringComparison.OrdinalIgnoreCase))
            {
                return outs == 0
                    ? Close(MovementDirection.Out, "تنفيذ الاستخراج", "يُقفل التصريح بعد التنفيذ.", "بانتظار التنفيذ")
                    : Done("تم التنفيذ");
            }

            return ins == 0
                ? Close(MovementDirection.In, "تنفيذ الإدخال", "يُقفل التصريح بعد التنفيذ.", "بانتظار التنفيذ")
                : Done("تم التنفيذ");
        }

        private static PermitExecutionStep Open(MovementDirection direction, string action, string hint, string progress) =>
            new()
            {
                Direction = direction,
                ActionLabel = action,
                Hint = hint,
                ClosesPermit = false,
                Progress = progress
            };

        private static PermitExecutionStep Close(MovementDirection direction, string action, string hint, string progress) =>
            new()
            {
                Direction = direction,
                ActionLabel = action,
                Hint = hint,
                ClosesPermit = true,
                Progress = progress
            };

        private static PermitExecutionStep Done(string progress) =>
            new() { Completed = true, Progress = progress };

        private static bool IsMaterials(string? classification)
        {
            var value = classification?.Trim() ?? "";
            return value.Equals(nameof(PermitClassification.Materials), StringComparison.OrdinalIgnoreCase)
                || value is "مواد" or "تصريح مواد";
        }

        private static bool IsVisitors(string classification) =>
            classification.Equals(nameof(PermitClassification.Visitors), StringComparison.OrdinalIgnoreCase)
            || classification is "زوار" or "تصريح زوار";

        private static bool IsCars(string classification) =>
            classification.Equals(nameof(PermitClassification.Cars), StringComparison.OrdinalIgnoreCase)
            || classification is "سيارات" or "مركبات" or "تصريح سيارات";
    }
}
