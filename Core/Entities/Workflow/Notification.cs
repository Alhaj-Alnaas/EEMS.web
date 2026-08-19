namespace Core.Entities
{
    /// <summary>
    /// In-app notification for a user, optionally linked to a permit or reader.
    /// Creation time uses <see cref="Base.createdOn"/> (do not add a duplicate CreatedOn).
    /// </summary>
    public class Notification : Base
    {
        public string UserId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsRead { get; set; }

        public Guid? RelatedPermitId { get; set; }
        public Permit? RelatedPermit { get; set; }

        public Guid? RelatedReaderId { get; set; }
        public Reader? RelatedReader { get; set; }
    }
}
