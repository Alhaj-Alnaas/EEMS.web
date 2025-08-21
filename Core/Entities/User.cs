
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


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

        public string Discriminator { get; set; }
        public string UserType { get; set; }


    }
}
