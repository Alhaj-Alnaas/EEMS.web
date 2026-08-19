using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Permit : Base
    {
        public string? reqDepartment { get; set; } = "";
        public string no { get; set; } = "";
        public string? classification { get; set; } = "";
        public string? type { get; set; } = "";
        public bool reqDepApproval { get; set; }=false;
        public bool secuSectionApproval { get; set; } = false;
        public bool PermintsSectionApproval { get; set; } = false;
        public Guid? GateId { get; set; }
        public Gate? Gate { get; set; }
        public char status { get; set; } = 'I';
        public string? statusDescription { get; set; } = "";
        public bool isClosed { get; set; }=false;
        public DateTime? closeOn { get; set; } = DateTime.Now;
        public string? moveFrom { get; set; } = "";
        public string? moveTo { get; set; } = "";
        public string? requoidedAs { get; set; } = "";
        public string? phoneNo { get; set; } = "";
        public string? OrgDescription { get; set; } = "";
        public bool IsTemp { get; set; } = false;
        public DateTime date { get; set; } = DateTime.Now;
        public string? hourOfEntry { get; set; } = "00:00"; 
        public ICollection<EquipMatiMovment> EquipmentsAndMatirials { get; set; } = new List<EquipMatiMovment>();
        public ICollection<CarMovment> Cars { get; set; } = new List<CarMovment>();
        public ICollection<ProcedureMovment> Procedures { get; set; } = new List<ProcedureMovment>();

        // ================= Unified Permit Management additions =================
        // Nullable/additive so existing EF migrations & legacy data are not broken.

        /// <summary>Effective validity window start for the unified permit model.</summary>
        public DateTime? ValidFrom { get; set; }

        /// <summary>Effective validity window end for the unified permit model.</summary>
        public DateTime? ValidTo { get; set; }

        /// <summary>String-backed <see cref="Core.Enums.BaseEnums.UnifiedPermitStatus"/>, mapped from/to legacy <see cref="status"/> via <see cref="Core.Common.StatusMapper"/>.</summary>
        public string? UnifiedStatus { get; set; }

        /// <summary>Optional configurable workflow this permit is routed through.</summary>
        public Guid? WorkflowDefinitionId { get; set; }
        public WorkflowDefinition? WorkflowDefinition { get; set; }

        /// <summary>Phase 1 employee who requested the permit (if applicable).</summary>
        public Guid? RequesterEmployeeId { get; set; }
        public Employee? RequesterEmployee { get; set; }

        /// <summary>Phase 1 employee hosting a visitor permit (if applicable).</summary>
        public Guid? HostEmployeeId { get; set; }
        public Employee? HostEmployee { get; set; }

        // TPT-style detail records (one of these applies depending on classification)
        public EquipmentPermitDetail? EquipmentDetail { get; set; }
        public VisitorPermitDetail? VisitorDetail { get; set; }
        public VehiclePermitDetail? VehicleDetail { get; set; }

        public ICollection<PermitApproval> Approvals { get; set; } = new List<PermitApproval>();
        public ICollection<MovementLog> MovementLogs { get; set; } = new List<MovementLog>();
    }
       
}
