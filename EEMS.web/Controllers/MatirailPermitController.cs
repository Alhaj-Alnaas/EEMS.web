using Core.Entities;
using Core.Interfaces.Services;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static Core.Enums.BaseEnums;
using PermitType = Core.Enums.BaseEnums.EnumPermitType;

public class MatirailPermitController : Controller
{
    private readonly IPermit _permitService;
    private readonly IDepartmentService _departmentService;
    private readonly UserManager<User> _userManager;
    private readonly IGates _gateService;

    public MatirailPermitController(IPermit permitService, IDepartmentService departmentService, UserManager<User> userManager , IGates gateServi)
    {
        _permitService = permitService;
        _departmentService = departmentService;
        _userManager = userManager;
        _gateService = gateServi;
    }
    

    [HttpGet]
    public async Task<IActionResult> CreateMatirialPermint()
    {
        // generate serial number
        string newSerial = await _permitService.GeneratePermitSerialNumberAsync("Materials");

        string responsibilityCode = HttpContext.Session.GetString("ResponsibilityCode"); 

        // fill departments
        var departments = await _departmentService.GetDepartmentsByResponsibilityAsync(responsibilityCode) ;

        var model = new PermitViewModel
        {
            No = newSerial,
            Date = DateTime.Now,
            HourOfEntry=DateTime.Now.ToShortTimeString(),
            Departments = departments,
            Cars = new List<CarMovmentViewModel>(),
            EquipmentsAndMaterials = new List<EquipMatiMovmentViewModel>(),
            ReturnDate = DateTime.Now,
        };
       
        return View("CreateMatirialPermint", model);

    }

    // حفظ البيانات
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMatirialPermint(PermitViewModel model)
    {
        //if (!ModelState.IsValid)
        //{
        //    // لو فيه مشكلة يعرض الصفحة مرة أخرى
        //    return View(model);
        //}

        
        var permit = new Permit
        {
            
            Id = Guid.NewGuid(),
            createdBy = HttpContext.Session.GetString("UserName"),
            createdOn = DateTime.Now,
            no = model.No,
            date = model.Date,
            hourOfEntry=model.HourOfEntry,
            type = model.Type.ToString(),
            classification = "Materials",
            IsTemp=model.IsTemp,    
            moveFrom = model.MoveFrom,
            moveTo = model.MoveTo,
            reqDepartment = model.ReqDepartment,
            phoneNo = model.PhoneNo,
            OrgDescription = model.OrgDescription,
            remarks = model.Notes,

        };

        // إضافة المواد  EquipmentsAndMaterials
        permit.EquipmentsAndMatirials = model.EquipmentsAndMaterials
            .Select(e => new EquipMatiMovment
            {
                description = e.ItemName,
                qty = e.Quantity,
                unit = e.Unit.ToString(),
            }).ToList();

        // إضافة السيارات Cars
        permit.Cars = model.Cars
            .Select(c => new CarMovment
            {
                // 1. ربط خصائص السيارة
                carType = c.CarType,          
                carNo = c.CarNo,             
                licenseNo = c.licenseNo,      
                name = c.DriverName,          
                nattiunality = c.driverNationality.ToString(),
                orginaization=model.OrgDescription
            }).ToList();

         // إضافة نوع الحركة
        permit.Procedures = new List<ProcedureMovment>
{
    new ProcedureMovment
    {
        procedureType =ProcedureType.Insert.ToString() ,
        donedBy = HttpContext.Session.GetString("FullName"),
        doneAs = HttpContext.Session.GetString("JobtypeName"),
        shift="A",
        doneOn = DateTime.Now
    }
};

        await _permitService.InsertPermitAsync(permit);

        TempData["Success"] = "تم إضافة التصريح بنجاح";
        return RedirectToAction("PermitIndex", "Permit");
    }

    [HttpGet]
    public async Task<IActionResult> EditMatirialPermint(Guid Id)
    {
        string responsibilityCode = HttpContext.Session.GetString("ResponsibilityCode");
 
        var permit = await _permitService.GetPermitByIdAsync(Id);
        if (permit == null)
        {
            return NotFound();
        }
        
        // حسب التصنيف نعرض صفحة مختلفة
        switch (permit.classification)
        {
            
            case "Materials":
                PermitViewModel viewModel = new PermitViewModel
                {
                    Id = permit.Id,
                    No = permit.no,
                    Classification = permit.classification,
                    Type = (PermitType)Enum.Parse(typeof(PermitType), permit.type),
                    OrgDescription = permit.OrgDescription,
                    IsTemp = permit.IsTemp,
                    MoveFrom = permit.moveFrom,
                    MoveTo = permit.moveTo,
                    Notes = permit.remarks,
                    RequoidedAs = permit.requoidedAs,
                    PhoneNo = permit.phoneNo,
                    Date = permit.date,
                    HourOfEntry = permit.hourOfEntry,
                    ReqDepartment = permit.reqDepartment, // تعبئة الجهة الطالبة

                    Cars = permit.Cars.Select(c => new CarMovmentViewModel
                    {
                        CarType = c.carType,
                        CarNo = c.carNo,
                        DriverName = c.name,
                        DriverOrg = c.orginaization,
                        driverNationality = (Nationality)Enum.Parse(typeof(Nationality), c.nattiunality),
                        licenseNo = c.licenseNo
                    }).ToList(),

                    EquipmentsAndMaterials = permit.EquipmentsAndMatirials.Select(e => new EquipMatiMovmentViewModel
                    {
                        ItemName = e.description,
                        Quantity = e.qty,
                        Unit =  (UnitType)Enum.Parse(typeof(UnitType), e.unit),
                        
                    }).ToList(),

                    Departments = await _departmentService.GetDepartmentsByResponsibilityAsync(responsibilityCode) 
                };

                return View("EditMatirialPermint", viewModel);

            // يمكن تضيف حالات أخرى لتصنيفات أخرى
            default:
                return RedirectToAction("PermitIndex", "Permit");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMatirialPermint(PermitViewModel model)
    {
      
            var responsibilityCode = HttpContext.Session.GetString("ResponsibilityCode");
            model.Departments = await _departmentService.GetDepartmentsByResponsibilityAsync(responsibilityCode) ;
          
        var permit = await _permitService.GetPermitByIdAsync(model.Id);
        if (permit == null) return NotFound();

        permit.updatedBy = HttpContext.Session.GetString("UserName");
        permit.updatedOn = DateTime.Now;
        permit.date = model.Date;
        permit.hourOfEntry = model.HourOfEntry;
        permit.type = model.Type.ToString();
        permit.IsTemp = model.IsTemp;
        permit.moveFrom = model.MoveFrom;
        permit.moveTo = model.MoveTo;
        permit.reqDepartment = model.ReqDepartment;
        permit.phoneNo = model.PhoneNo;
        permit.OrgDescription = model.OrgDescription;
        permit.remarks = model.Notes;

        // تحديث المواد
        permit.EquipmentsAndMatirials.Clear();
        permit.EquipmentsAndMatirials = model.EquipmentsAndMaterials.Select(e => new EquipMatiMovment
        {
            description = e.ItemName,
            qty = e.Quantity,
            unit = e.Unit.ToString(),
        }).ToList();

        // تحديث السيارات
        permit.Cars.Clear();
        permit.Cars = model.Cars.Select(c => new CarMovment
        {
            carType = c.CarType,
            carNo = c.CarNo,
            licenseNo = c.licenseNo,
            name = c.DriverName,
            nattiunality = c.driverNationality.ToString(),
            orginaization = model.OrgDescription
        }).ToList();

        // إضافة نوع الحركة
        permit.Procedures = new List<ProcedureMovment>
{
    new ProcedureMovment
    {
        procedureType = ProcedureType.Update.ToString(),
        donedBy = HttpContext.Session.GetString("FullName"),
        doneAs = HttpContext.Session.GetString("JobtypeName"),
        shift="A",
        doneOn = DateTime.Now
    }
};
        await _permitService.UpdatePermitAsync(permit);

        return RedirectToAction("PermitIndex", "Permit");
    }


    public async Task<IActionResult> Details(Guid Id)
    {
        var permit = await _permitService.GetPermitByIdAsync(Id);
        if (permit == null)
        {
            return NotFound();
        }
        var department = await _departmentService.GetSingleDepartmentByResponsibilityAsync(permit.reqDepartment) ;

        var departmentName = department?.DepartmentName ?? "غير معروف";
       
        var viewModel = new PermitViewModel
        {
            Id = permit.Id,
            No = permit.no,
            Classification = permit.classification,
            Type = (PermitType)Enum.Parse(typeof(PermitType), permit.type),
            OrgDescription = permit.OrgDescription,
            IsTemp = permit.IsTemp,
            MoveFrom = permit.moveFrom,
            MoveTo = permit.moveTo,
            Notes = permit.remarks,
            RequoidedAs = permit.requoidedAs,
            PhoneNo = permit.phoneNo,
            Date = permit.date,
            HourOfEntry = permit.hourOfEntry,
            ReqDepartment = departmentName,
            //ReqDepartment = permit.reqDepartment,
            Cars = permit.Cars.Select(c => new CarMovmentViewModel
            {
                CarType = c.carType,
                CarNo = c.carNo,
                DriverName = c.name,
                DriverOrg = c.orginaization,
                driverNationality = (Nationality)Enum.Parse(typeof(Nationality), c.nattiunality),
                licenseNo = c.licenseNo
            }).ToList(),

            EquipmentsAndMaterials = permit.EquipmentsAndMatirials.Select(e => new EquipMatiMovmentViewModel
            {
                ItemName = e.description,
                Quantity = e.qty,
                Unit = (UnitType)Enum.Parse(typeof(UnitType), e.unit),

            }).ToList(),

            Procedures = permit.Procedures.Select(p => new ProcedureMovmentViewModel
            {
                ProcedureType = p.procedureType,
                DonedBy = p.donedBy,
                DoneOn = p.doneOn
            }).ToList(),

           Gates = await _gateService.GetGatesByPermitTypeAsync(3)
        };

        return View("ViewMatirialPermint", viewModel);
    }

}
