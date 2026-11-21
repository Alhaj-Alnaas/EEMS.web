using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Resolves who approves a configured path step from the entrant's responsibility code.
    /// A non-leader always starts with the direct supervisor, even when the permit type needs no other approval.
    /// Configured steps apply only when they sit strictly above that supervisor. A leader starts at the next enabled step.
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

        public static Guid DirectSupervisorStepId(Guid workflowId)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("pms-direct-supervisor-gate:" + workflowId.ToString("N")));
            hash[7] = (byte)((hash[7] & 0x0F) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash.AsSpan(0, 16));
        }

        /// <summary>True when a configured step is strictly above the entrant's direct supervisor.</summary>
        public static bool ComesAfterDirectSupervisor(WorkflowStepType type, string? directCode, IReadOnlySet<string> knownUnits)
        {
            if (type == WorkflowStepType.HigherSupervisor)
                return true;

            var rank = LadderRank(type);
            if (rank == null || string.IsNullOrEmpty(directCode))
                return false;

            return rank.Value > LadderRankOfCode(directCode, knownUnits);
        }

        public static int? LadderRank(WorkflowStepType type) => type switch
        {
            WorkflowStepType.SectionHead => 0,
            WorkflowStepType.AdministrationManager => 1,
            WorkflowStepType.GeneralManager => 2,
            WorkflowStepType.SectorSupervisor => 3,
            WorkflowStepType.ElectronicMonitoring => 4,
            WorkflowStepType.SecurityAffairs => 5,
            WorkflowStepType.GuardSection => 6,
            _ => null
        };

        public static int LadderRankOfCode(string? code, IReadOnlySet<string> knownUnits)
        {
            var current = Normalize(code);
            if (Same(current, "45012"))
                return 4;
            if (Same(current, SecurityAffairsCode))
                return 5;
            if (Same(current, "45011"))
                return 6;

            return EffectiveRank(current, knownUnits) switch
            {
                OrgRank.Administration => 1,
                OrgRank.General => 2,
                OrgRank.Sector => 3,
                _ => 0
            };
        }

        public static string? StepTarget(
            WorkflowStepType type,
            string? entrantCode,
            bool entrantIsLeader,
            IReadOnlySet<string> leaderCodes,
            IReadOnlySet<string>? knownUnits = null)
        {
            if (type == WorkflowStepType.DirectSupervisor)
                return DirectSupervisor(entrantCode, entrantIsLeader, leaderCodes);

            if (type == WorkflowStepType.HigherSupervisor)
            {
                if (BelongsTo(entrantCode, SecurityAffairsCode))
                    return null;

                return HigherSupervisor(entrantCode, entrantIsLeader, leaderCodes);
            }

            var known = knownUnits ?? leaderCodes;
            if (BelongsTo(entrantCode, SecurityAffairsCode))
                return SecurityTarget(type, entrantCode, entrantIsLeader, leaderCodes);

            if (RankOf(type) is OrgRank stepRank)
                return OrgTarget(stepRank, entrantCode, entrantIsLeader, leaderCodes, known);

            var fixedCode = FixedSecurityCode(type);
            return Occupied(fixedCode, leaderCodes);
        }

        public static bool WatchesOrganization(
            string? viewerCode,
            string? entrantCode,
            bool entrantIsLeader,
            IReadOnlySet<string> leaderCodes,
            IReadOnlySet<string> knownUnits)
        {
            foreach (var type in new[]
            {
                WorkflowStepType.SectionHead,
                WorkflowStepType.AdministrationManager,
                WorkflowStepType.GeneralManager,
                WorkflowStepType.SectorSupervisor
            })
            {
                if (Same(viewerCode, StepTarget(type, entrantCode, entrantIsLeader, leaderCodes, knownUnits)))
                    return true;
            }

            return false;
        }

        private static string? OrgTarget(
            OrgRank stepRank,
            string? entrantCode,
            bool entrantIsLeader,
            IReadOnlySet<string> leaderCodes,
            IReadOnlySet<string> knownUnits)
        {
            var code = Normalize(entrantCode);
            if (code.Length < 5)
                return null;

            var effective = EffectiveRank(code, knownUnits);
            if (stepRank < effective)
                return null;

            if (stepRank == effective)
                return entrantIsLeader ? null : Occupied(code, leaderCodes);

            return Occupied(AncestorAt(code, stepRank, knownUnits), leaderCodes);
        }

        private static string? SecurityTarget(
            WorkflowStepType type,
            string? entrantCode,
            bool entrantIsLeader,
            IReadOnlySet<string> leaderCodes)
        {
            var fixedCode = FixedSecurityCode(type);
            if (fixedCode == null)
                return null;

            var own = OwnSecurityStep(Normalize(entrantCode));
            if (entrantIsLeader && own != null && SecurityOrder(type) <= SecurityOrder(own.Value))
                return null;

            return Occupied(fixedCode, leaderCodes);
        }

        /// <summary>A section whose parent administration has no staff stands in for that administration.</summary>
        private static OrgRank EffectiveRank(string code, IReadOnlySet<string> knownUnits)
        {
            var rank = StructuralRank(code);
            if (rank == OrgRank.Section && !Exists(Parent(code), knownUnits))
                return OrgRank.Administration;
            return rank;
        }

        private static string? AncestorAt(string code, OrgRank rank, IReadOnlySet<string> knownUnits)
        {
            var current = Parent(code);
            while (!string.IsNullOrEmpty(current))
            {
                if (StructuralRank(current) == rank)
                    return Exists(current, knownUnits) ? current : null;
                current = Parent(current);
            }

            return null;
        }

        private static string? Occupied(string? code, IReadOnlySet<string> leaderCodes)
        {
            var current = Normalize(code);
            return current.Length >= 5 && HasLeader(leaderCodes, current) ? current : null;
        }

        private static bool Exists(string? code, IReadOnlySet<string>? knownUnits)
        {
            var current = Normalize(code);
            if (current.Length < 5)
                return false;
            if (knownUnits == null)
                return true;
            return knownUnits.Contains(current);
        }

        private static WorkflowStepType? OwnSecurityStep(string code)
        {
            if (Same(code, "45012"))
                return WorkflowStepType.ElectronicMonitoring;
            if (Same(code, SecurityAffairsCode))
                return WorkflowStepType.SecurityAffairs;
            if (Same(code, "45011"))
                return WorkflowStepType.GuardSection;
            return null;
        }

        private static int SecurityOrder(WorkflowStepType type) => type switch
        {
            WorkflowStepType.ElectronicMonitoring => 1,
            WorkflowStepType.SecurityAffairs => 2,
            WorkflowStepType.GuardSection => 3,
            _ => 0
        };

        private static string? FixedSecurityCode(WorkflowStepType type) => type switch
        {
            WorkflowStepType.ElectronicMonitoring => "45012",
            WorkflowStepType.SecurityAffairs => SecurityAffairsCode,
            WorkflowStepType.GuardSection => "45011",
            _ => null
        };

        private static OrgRank? RankOf(WorkflowStepType type) => type switch
        {
            WorkflowStepType.SectionHead => OrgRank.Section,
            WorkflowStepType.AdministrationManager => OrgRank.Administration,
            WorkflowStepType.GeneralManager => OrgRank.General,
            WorkflowStepType.SectorSupervisor => OrgRank.Sector,
            _ => null
        };

        private static OrgRank StructuralRank(string code)
        {
            if (IsSector(code))
                return OrgRank.Sector;
            if (IsGeneralAdministration(code))
                return OrgRank.General;
            if (IsAdministration(code))
                return OrgRank.Administration;
            return OrgRank.Section;
        }

        private enum OrgRank
        {
            Section = 0,
            Administration = 1,
            General = 2,
            Sector = 3
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
