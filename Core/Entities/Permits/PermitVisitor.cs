namespace Core.Entities
{
    /// <summary>One authorized visitor on a visitor permit.</summary>
    public class PermitVisitor : Base
    {
        public Guid PermitId { get; set; }
        public Permit? Permit { get; set; }

        public string Name { get; set; } = "";
        public string? IdNumber { get; set; }
        public string? Nationality { get; set; }
    }
}
