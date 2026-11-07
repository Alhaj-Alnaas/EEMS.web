using Core.Entities;

namespace Core.Common
{
    public static class PermitText
    {
        public static string Purpose(Permit? permit)
        {
            if (permit == null)
                return "";
            if (!string.IsNullOrWhiteSpace(permit.EquipmentDetail?.Description))
                return permit.EquipmentDetail.Description.Trim();
            if (!string.IsNullOrWhiteSpace(permit.VehicleDetail?.remarks))
                return permit.VehicleDetail.remarks.Trim();
            if (!string.IsNullOrWhiteSpace(permit.VisitorDetail?.Purpose))
                return permit.VisitorDetail.Purpose.Trim();
            return "";
        }
    }
}
