namespace Core.Entities.DTOs
{
    public class PermitActionsVisibility
    {
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanReturn { get; set; }
        public bool CanClose { get; set; }
        public bool CanDelete { get; set; }
    }
}
