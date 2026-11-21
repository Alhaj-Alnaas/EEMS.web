namespace Core.Models
{
    public sealed class LaborActor
    {
        public string? UserName { get; init; }
        public string? FullName { get; init; }
    }

    public sealed class LaborCompanyInput
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public string? Specialty { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime? ContractStart { get; set; }
        public DateTime? ContractEnd { get; set; }
    }

    public sealed class LaborCompanyRow
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Address { get; init; }
        public string? Specialty { get; init; }
        public string Status { get; init; } = "";
        public DateTime? ContractStart { get; init; }
        public DateTime? ContractEnd { get; init; }
        public string PermitCode { get; init; } = "";
        public int WorkerCount { get; init; }
    }

    public sealed class LaborWorkerInput
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string FullName { get; set; } = "";
        public string? Nationality { get; set; }
        public string? PassportNo { get; set; }
        public string Status { get; set; } = "Active";
        public string? Address { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class LaborWorkerRow
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public string CompanyName { get; init; } = "";
        public string CompanyStatus { get; init; } = "";
        public string PermitNo { get; init; } = "";
        public string FullName { get; init; } = "";
        public string? Nationality { get; init; }
        public string? PassportNo { get; init; }
        public string Status { get; init; } = "";
        public string? Address { get; init; }
        public string? Notes { get; init; }
        public DateTime? PrintedAt { get; init; }
    }

    public sealed class LaborSheetRow
    {
        public int RowNumber { get; init; }
        public string Name { get; init; } = "";
        public string? Nationality { get; init; }
        public string? PassportNo { get; init; }
        public string? StatusText { get; init; }
        public string? Address { get; init; }
        public string? Notes { get; init; }
    }

    public sealed class ExternalLaborCompany
    {
        public string Number { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Address { get; init; }
        public string? Specialty { get; init; }
    }

    public sealed class ExternalCompanyLookup
    {
        public bool Available { get; init; }
        public string? Message { get; init; }
        public List<ExternalLaborCompany> Items { get; init; } = new();
    }

    public sealed class LaborExternalImportResult
    {
        public int Added { get; init; }
        public int Skipped { get; init; }
    }

    public sealed class LaborBatchLineRow
    {
        public Guid Id { get; init; }
        public string Action { get; init; } = "";
        public string FullName { get; init; } = "";
        public string? PassportNo { get; init; }
        public string? CurrentPermitNo { get; init; }
        public string? ProposedPermitNo { get; init; }
        public string? CurrentStatus { get; init; }
        public string? ProposedStatus { get; init; }
    }

    public sealed class LaborBatchDetail
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public string CompanyName { get; init; } = "";
        public string Mode { get; init; } = "";
        public string State { get; init; } = "";
        public string? FileName { get; init; }
        public DateTime CreatedOn { get; init; }
        public DateTime? AppliedOn { get; init; }
        public List<LaborBatchLineRow> Lines { get; init; } = new();
    }

    public sealed class LaborFollowRow
    {
        public Guid WorkerId { get; init; }
        public string CompanyName { get; init; } = "";
        public string CompanyNumber { get; init; } = "";
        public string PermitNo { get; init; } = "";
        public string FullName { get; init; } = "";
        public string? Nationality { get; init; }
        public string? PassportNo { get; init; }
        public string Status { get; init; } = "";
        public string? Address { get; init; }
        public string? Notes { get; init; }
    }
}
