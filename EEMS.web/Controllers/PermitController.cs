using Core.Entities;
using Core.Interfaces.Services;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EEMS.web.Controllers
{
    public class PermitController : Controller

    {
         private readonly IPermit _permitService;
    private readonly IDepartmentService _departmentService;
    private readonly UserManager<User> _userManager;

    public PermitController(IPermit permitService, IDepartmentService departmentService, UserManager<User> userManager)
    {
        _permitService = permitService;
        _departmentService = departmentService;
        _userManager = userManager;
    }

    // GET: Permit/Index
    public async Task<IActionResult> PermitIndex()
        {
            string userId = HttpContext.Session.GetString("UserName");
            var user = await _userManager.FindByNameAsync(userId);

            // get permit  
            var permits = await _permitService.GetAllPermitAsync(user);

            // تحويل إلى ViewModel
            //var permitVMs = permits.Select(p => new Permit
            //{
            //    Id = p.Id,
            //    No = p.no,
            //    Classification = p.classification,
            //    Type = p.type,
            //    OrgDescription = p.OrgDescription,
            //    IsTemp = p.IsTemp,
            //    //ReturnDate = p.ReturnDate,
            //    MoveFrom = p.moveFrom,
            //    MoveTo = p.moveTo,
            //    Notes = p.remarks,
            //    RequoidedAs = p.requoidedAs,
            //    PhoneNo = p.phoneNo,
            //    Date = p.date,
            //    HourOfEntry = p.hourOfEntry,
            //}).ToList();
            return View(permits);
        }

        // GET: Permit/Details/{id}
        public async Task<IActionResult> Details(Guid id)
        {
            var permit = await _permitService.GetPermitByIdAsync(id);
            if (permit == null) return NotFound();
            return View(permit);
        }

        // GET: Permit/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var permit = await _permitService.GetPermitByIdAsync(id);
            if (permit == null) return NotFound();
            return View(permit);
        }
    }
}
