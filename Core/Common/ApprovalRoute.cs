using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Resolves who approves a configured path step from the entrant's responsibility code.
    /// A leader is his own direct supervisor, so that step is skipped.
    /// From general manager upward, and for an administration with no general administration, there is no separate higher supervisor.
    /// A permit entered from security affairs or one of its sections skips both supervisor steps and keeps electronic monitoring, the security-affairs director, then the guard-section head.
    /// </summary>
    public static class ApprovalRoute
    {
        public static string Normalize(string? code)
        {
            var trimmed = (code ?? "").Trim();
            return trimmed.Length >= 5 ? trimmed[..5] : trimmed;
        }

        public static string? Parent(string? code)
        {
            var current = Normalize(code);
            if (current.Length < 5)
                return null;
            if (!current.EndsWith('0'))
                return current[..4] + "0";
            if (!current.EndsWith("00"))
                return current[..3] + "00";
            if (!current.EndsWith("000"))
                return current[..2] + "000";
            return null;
        }

        public static List<string> Upward(string? code)
        {
            var chain = new List<string>();
            var current = Normalize(code);
            while (!string.IsNullOrEmpty(current))
            {
                chain.Add(current);
                var parent = Parent(current);
                if (parent == null || chain.Contains(parent))
                    break;
                current = parent;
            }
            return chain;
        }

        public static string? DirectSupervisor(string? entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaderCodes)
        {
            if (entrantIsLeader)
                return null;

            var chain = Upward(entrantCode);
            for (var i = 0; i < chain.Count; i++)
            {
                if (HasLeader(leaderCodes, chain[i]))
                    return chain[i];
            }
            return null;
        }

        public static string? HigherSupervisor(string? entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaderCodes)
        {
            if (HeadCoversHigherSupervisor(entrantCode))
                return null;

            var chain = Upward(entrantCode);
            var start = entrantIsLeader ? 1 : AfterDirect(chain, entrantCode, leaderCodes);
            for (var i = start; i < chain.Count; i++)
            {
                if (HasLeader(leaderCodes, chain[i]))
                    return chain[i];
            }
            return null;
        }

        public static string? StepTarget(WorkflowStepType type, string? entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaderCodes)
        {
            if (BelongsTo(entrantCode, SecurityAffairsCode)
                && type is WorkflowStepType.DirectSupervisor or WorkflowStepType.HigherSupervisor)
                return null;

            return type switch
            {
                WorkflowStepType.DirectSupervisor => DirectSupervisor(entrantCode, entrantIsLeader, leaderCodes),
                WorkflowStepType.HigherSupervisor => HigherSupervisor(entrantCode, entrantIsLeader, leaderCodes),
                WorkflowStepType.ElectronicMonitoring => "45012",
                WorkflowStepType.SecurityAffairs => SecurityAffairsCode,
                WorkflowStepType.GuardSection => "45011",
                _ => null
            };
        }

        /// <summary>
        /// An administration, general administration, or sector has no separate higher supervisor.
        /// The head of that division is the higher supervisor.
        /// </summary>
        public static bool HeadCoversHigherSupervisor(string? code)
        {
            var current = Normalize(code);
            if (current.Length < 5)
                return false;
            if (IsSector(current) || IsGeneralAdministration(current))
                return true;
            return IsAdministration(current) && !HasGeneralAdministration(current);
        }

        public static bool IsSection(string code) =>
            code.Length >= 5 && !code.EndsWith('0');

        public static bool IsAdministration(string code) =>
            code.Length >= 5 && code.EndsWith('0') && !code.EndsWith("00");

        public static bool IsGeneralAdministration(string code) =>
            code.Length >= 5 && code.EndsWith("00") && !code.EndsWith("000");

        public static bool IsSector(string code) =>
            code.Length >= 5 && code.EndsWith("000");

        public static bool HasGeneralAdministration(string? code)
        {
            var current = Normalize(code);
            if (!IsAdministration(current))
                return false;
            var parent = Parent(current);
            return parent != null && IsGeneralAdministration(parent);
        }

        private static int AfterDirect(List<string> chain, string? entrantCode, IReadOnlySet<string> leaderCodes)
        {
            var direct = DirectSupervisor(entrantCode, false, leaderCodes);
            if (direct == null)
                return chain.Count;
            var index = chain.FindIndex(item => Same(item, direct));
            return index < 0 ? chain.Count : index + 1;
        }

        public static bool HasLeader(IReadOnlySet<string> leaderCodes, string code) =>
            leaderCodes.Contains(Normalize(code));

        public static bool Same(string? left, string? right) =>
            string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

        public const string SecurityAffairsCode = "45010";

        public static bool BelongsTo(string? code, string unit) =>
            Upward(code).Any(item => Same(item, unit));
    }
}
