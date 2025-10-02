using Core.Entities;
using EEMS.web;
using Core.Entities.DTOs;
using Core.Interfaces.Services;
using DataAccess;
using EEMS.Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using EEMS.web.ViewModels;
namespace Services
{
    public class PermitServices : IPermit
    {
        protected readonly DataContext _dataContext;
        private readonly IUnitOfWork _unitOfWork;

        public PermitServices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // توليد رقم التصريح حسب نوعه
        public async Task<string> GeneratePermitSerialNumberAsync(string permitType)
        {
            if (string.IsNullOrWhiteSpace(permitType))
                throw new ArgumentException("permitType is required", nameof(permitType));

            // السنة بصيغة 2 رقم
            var now = DateTime.Now;
            string year = now.ToString("yy");

            // كود نوع التصريح
            string typeCode = permitType.ToLower() switch
            {
                "materials" => "1",
                "visitors" => "2",
                "cars" => "3",
                _ => permitType.Substring(0, 1).ToUpperInvariant()
            };

            // جلب آخر تصريح من قاعدة البيانات (فلترة بحسب النوع + نفس السنة إن أردت)
            var lastPermit = await _unitOfWork.Permits.GetQueryable()
                .Where(p => p.classification == permitType && p.createdOn.Year == DateTime.Now.Year) // && p.date.Year == DateTime.Now.Year
                .OrderByDescending(p => p.createdOn) // أفضل من OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            int lastNumber = 0;

            if (lastPermit != null && !string.IsNullOrWhiteSpace(lastPermit.no))
            {
                // نحاول استخراج آخر 4 أرقام من نهاية الحقل NO (مثال: "24-M-0003")
                var match = Regex.Match(lastPermit.no, @"(\d{1,})$");
                if (match.Success)
                {
                    int.TryParse(match.Value, out lastNumber);
                }
            }

            lastNumber++; // الرقم الجديد

            string newSerial = $"{year}-{typeCode}-{lastNumber:D4}";
            return newSerial;


            //if (string.IsNullOrEmpty(permitType))
            //    throw new ArgumentException("Permit type cannot be null or empty", nameof(permitType));

            //// استخراج آخر تصريح لهذا النوع
            //var lastPermit =  _unitOfWork.Permits.GetQueryable()
            //    .Where(p => p.type == permitType)
            //    .OrderByDescending(p => p.Id)
            //    .FirstOrDefaultAsync();

            //int lastNumber = 0;
            //if (lastPermit != null)
            //{
            //    var parts = lastPermit.Result.no.Split('-'); // YY-T-0001
            //    if (parts.Length == 3)
            //        int.TryParse(parts[2], out lastNumber);
            //}

            //lastNumber++;
            //string year = DateTime.Now.ToString("yy");

            //// تعيين كود النوع
            //string typeCode;
            //switch (permitType.ToLower())
            //{
            //    case "materials": typeCode = "M"; break;
            //    case "visitors": typeCode = "V"; break;
            //    case "cars": typeCode = "C"; break;
            //    default: typeCode = permitType.Substring(0, 1).ToUpperInvariant(); break;
            //}

            //string newSerial = $"{year}-{typeCode}-{lastNumber:D4}";
            //return newSerial;
        }

        //// جلب الإدارات حسب ResponsibilityCode من SP
        //public async Task<List<DepartmentDto>> GetDepartmentsByResponsibilityAsync(string responsibilityCode)
        //{
        //    return await  _dataContext..FromSqlRaw("sp_show_Req_dep {0}"
        //         , responsibilityCode);
            
        //       // return departments;
            
        //}

        public async Task InsertPermitAsync(Permit permit)
        {
            if (permit == null)
            {
                throw new ArgumentNullException(nameof(permit));
            }

            _unitOfWork.Permits.Insert(permit);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdatePermitAsync(Permit permit)
        {
            if (permit == null)
            {
                throw new ArgumentNullException(nameof(permit));
            }

            var existingPermit = await _unitOfWork.Permits.GetByIdAsync(permit.Id);
            if (existingPermit == null) throw new Exception("التصريح غير موجود");

            // تحديث الخصائص الأساسية
            existingPermit.reqDepartment = permit.reqDepartment;
            existingPermit.no = permit.no;
            existingPermit.classification = permit.classification;
            existingPermit.type = permit.type;
            existingPermit.reqDepApproval = permit.reqDepApproval;
           // existingPermit.secuDepApproval = permit.secuDepApproval;
            existingPermit.gateId = permit.gateId;
            existingPermit.status = permit.status;
            existingPermit.statusDescription = permit.statusDescription;
            existingPermit.isClosed = permit.isClosed;
            existingPermit.closeOn = permit.closeOn;
            existingPermit.moveFrom = permit.moveFrom;
            existingPermit.moveTo = permit.moveTo;
            existingPermit.requoidedAs = permit.requoidedAs;
            existingPermit.phoneNo = permit.phoneNo;
            existingPermit.date = permit.date;
            existingPermit.hourOfEntry = permit.hourOfEntry;
            existingPermit.updatedOn = DateTime.Now;
            existingPermit.updatedBy = permit.updatedBy;

            _unitOfWork.Permits.Update(existingPermit);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeletePermitAsync(Permit permit)
        {
            if (permit == null)
            {
                throw new ArgumentNullException(nameof(permit));
            }

            _unitOfWork.Permits.Delete(permit);
            await _unitOfWork.SaveAsync();
        }

        async Task<Permit> IPermit.GetPermitByIdAsync(Guid PermitId)
        {
            var permit = await _unitOfWork.Permits
       .GetQueryable()                
       .Include(p => p.Cars)         
       .Include(p => p.EquipmentsAndMatirials) 
       .FirstOrDefaultAsync(p => p.Id == PermitId);

            return permit;
        }

        Task<Permit> IPermit.GetPermitByTypeAsync(string permitType)
        {
            throw new NotImplementedException();
        }

        Task<List<Permit>> IPermit.GetUnApprovedPermitAsync(string UserId)
        {
            throw new NotImplementedException();
        }

        Task<List<Permit>> IPermit.GetPendingPermitAsync(string UserId, string RespCode)
        {
            throw new NotImplementedException();
        }

        Task<List<Permit>> IPermit.GetClosedPermitAsync(string UserId)
        {
            throw new NotImplementedException();
        }

        public async Task<List<Permit>> GetAllPermitAsync(User user)
        {
            var query = _unitOfWork.Permits.GetQueryable();

            if (user.ResponsibilityCode != "45010")
            {
                query = query.Where(p => p.createdBy == user.UserName);
            }

            return await query
                .OrderByDescending(p => p.createdOn)
                .ToListAsync();
        }



        Task<List<Permit>> IPermit.SearchByAsync(List<Parameter> parameters)
        {
            throw new NotImplementedException();
        }


        //public async Task<Permit> GetPermitByIdAsync(string permitId)
        //{
        //    if (string.IsNullOrEmpty(permitId))
        //    {
        //        throw new ArgumentException("Permit ID cannot be null or empty", nameof(permitId));
        //    }

        //    return await _unitOfWork.Permits.GetByIdAsync(
        //        filter: p => p.no == permitId,
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<Permit> GetPermitByTypeAsync(string permitType)
        //{
        //    if (string.IsNullOrEmpty(permitType))
        //    {
        //        throw new ArgumentException("Permit type cannot be null or empty", nameof(permitType));
        //    }

        //    return await _unitOfWork.Permits.GetAllAsync(
        //        filter: p => p.type == permitType,
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<List<Permit>> GetUnApprovedPermitAsync(string userId)
        //{
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        //    }

        //    return await _unitOfWork.Permits.GetAllAsync(
        //        filter: p => !p.reqDepApproval && !p.secuDepApproval && p.reqDepartment == userId,
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<List<Permit>> GetPendingPermitAsync(string userId, string respCode)
        //{
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        //    }

        //    if (string.IsNullOrEmpty(respCode))
        //    {
        //        throw new ArgumentException("Response code cannot be null or empty", nameof(respCode));
        //    }

        //    return await _unitOfWork.Permits.GetAllAsync(
        //        filter: p => (p.reqDepApproval || p.secuDepApproval) &&
        //                   (p.reqDepartment == userId || p.reqDepartment == respCode),
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<List<Permit>> GetClosedPermitAsync(string userId)
        //{
        //if (string.IsNullOrEmpty(userId))
        //{
        //    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        //}

        //return await _unitOfWork.Permits.GetAllAsync(
        //    filter: p => p.isClosed && p.reqDepartment == userId,
        //    include: q => q.Include(p => p.Procedures)
        //                  .Include(p => p.Cars)
        //                  .Include(p => p.Humans)
        //                  .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<List<Permit>> GetAllPermitAsync(string userId)
        //{
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        //    }

        //    return await _unitOfWork.Permits.GetAllAsync(
        //        filter: p => p.reqDepartment == userId,
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));
        //}

        //public async Task<List<Permit>> SearchByAsync(List<Parameter> parameters)
        //{
        //    if (parameters == null || !parameters.Any())
        //    {
        //        return new List<Permit>();
        //    }

        //    // بناء استعلام ديناميكي بناءً على المعلمات الممررة
        //    var query = _unitOfWork.Permits.GetQueryable(
        //        include: q => q.Include(p => p.Procedures)
        //                      .Include(p => p.Cars)
        //                      .Include(p => p.Humans)
        //                      .Include(p => p.EquipmentsAndMatirials));

        //foreach (var param in parameters)
        //{
        //    switch (param.Name.ToLower())
        //    {
        //        case "department":
        //            query = query.Where(p => p.reqDepartment.Contains(param.Value));
        //            break;
        //        case "classification":
        //            query = query.Where(p => p.classification.Contains(param.Value));
        //            break;
        //        case "type":
        //            query = query.Where(p => p.type.Contains(param.Value));
        //            break;
        //        case "status":
        //            if (param.Value.Length == 1)
        //                query = query.Where(p => p.status == param.Value[0]);
        //            break;
        //        case "date":
        //            if (DateTime.TryParse(param.Value, out DateTime date))
        //                query = query.Where(p => DbFunctions.TruncateTime(p.date) == date.Date);
        //            break;
        //        case "movefrom":
        //            query = query.Where(p => p.moveFrom.Contains(param.Value));
        //            break;
        //        case "moveto":
        //            query = query.Where(p => p.moveTo.Contains(param.Value));
        //            break;
        //            // يمكنك إضافة المزيد من الحقول حسب الحاجة
        //    }
        //}

        //    return await query.ToListAsync();
        //}

        //public async Task<string> GeneratePermitSerialNumberAsync(string permitType)
        //{
        //    if (string.IsNullOrEmpty(permitType))
        //    {
        //        throw new ArgumentException("Permit type cannot be null or empty", nameof(permitType));
        //    }

        //    // الحصول على آخر رقم تسلسلي لنوع التصريح المحدد
        //    var lastPermit = await _unitOfWork.Permits.GetAllAsync(
        //        filter: p => p.type == permitType,
        //        orderBy: q => q.OrderByDescending(p => p.no));

        //    int lastNumber = 0;
        //    if (lastPermit != null)
        //    {
        //        // استخراج الرقم الرقمي من آخر تصريح
        //        string numericPart = new string(lastPermit.no.Where(char.IsDigit).ToArray());
        //        int.TryParse(numericPart, out lastNumber);
        //    }

        //    // زيادة الرقم بمقدار 1
        //    lastNumber++;

        //    // إنشاء الرقم التسلسلي الجديد (يمكن تعديل التنسيق حسب المتطلبات)
        //    string newSerialNumber = $"{permitType}-{DateTime.Now:yyyyMMdd}-{lastNumber.ToString("D4")}";

        //    return newSerialNumber;
        //}
    }
}


