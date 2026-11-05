
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Security;


namespace Core.Entities
{
    public class User: IdentityUser
    {
        public string FileNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }

        public int? RespCodeId { get; set; }
        public int? JobCatId { get; set; }
        public int? DesignationId { get; set; }

        public string JobStatus { get; set; }
        public string JobtypeName { get; set; }
        public string ResponsibilityCode { get; set; }

        /// <summary>Legacy discriminator kept for Identity compatibility; prefer AccessLevel.</summary>
        public string Discriminator { get; set; } = "User";

        /// <summary>Legacy coarse type; kept nullable during transition. Prefer AccessLevel.</summary>
        public string? UserType { get; set; }

        public Guid? AccessLevelId { get; set; }

        [ForeignKey(nameof(AccessLevelId))]
        public AccessLevel? AccessLevel { get; set; }
    }
}
