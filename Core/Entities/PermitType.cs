namespace Core.Entities
{
    public class PermitType : Base
    {
        public string Name { get; set; } = "";
        public ICollection<Gate> Gates { get; set; } = new List<Gate>();
    }
}
