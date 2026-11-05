namespace Core.Entities.Security
{
    public class AccessLevelTask
    {
        public Guid AccessLevelId { get; set; }
        public AccessLevel AccessLevel { get; set; } = null!;

        public Guid SystemTaskId { get; set; }
        public SystemTask SystemTask { get; set; } = null!;
    }
}
