using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Resolves who approves a configured path step from the entrant's responsibility code.
    /// A missing leadership level is skipped by walking up: section, administration, general administration, sector.
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
            var chain = Upward(entrantCode);
            var start = entrantIsLeader ? 1 : 0;
            for (var i = start; i < chain.Count; i++)
            {
                if (HasLeader(leaderCodes, chain[i]))
                    return chain[i];
            }
            return null;
        }

        public static string? HigherSupervisor(string? entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaderCodes)
        {
            var direct = DirectSupervisor(entrantCode, entrantIsLeader, leaderCodes);
            if (direct == null)
                return null;

            var chain = Upward(entrantCode);
            var index = chain.FindIndex(c => Same(c, direct));
            for (var i = index + 1; i < chain.Count; i++)
            {
                if (HasLeader(leaderCodes, chain[i]))
                    return chain[i];
            }
            return null;
        }

        public static string? StepTarget(WorkflowStepType type, string? entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaderCodes) =>
            type switch
            {
                WorkflowStepType.DirectSupervisor => DirectSupervisor(entrantCode, entrantIsLeader, leaderCodes),
                WorkflowStepType.HigherSupervisor => HigherSupervisor(entrantCode, entrantIsLeader, leaderCodes),
                WorkflowStepType.ElectronicMonitoring => "45012",
                WorkflowStepType.SecurityAffairs => "45010",
                WorkflowStepType.GuardSection => "45011",
                _ => null
            };

        public static bool HasLeader(IReadOnlySet<string> leaderCodes, string code) =>
            leaderCodes.Contains(Normalize(code));

        public static bool Same(string? left, string? right) =>
            string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }
}
