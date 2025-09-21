using Core.Entities;
using EEMS.web.Data;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EEMS.Web.Controllers
{
    public class PermitController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PermitController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 📌 GET: Permit/Create?type=materials
        [HttpGet]
        public IActionResult Create(string type)
        {
            var model = new PermitViewModel
            {
                Type = type,
                Date = DateTime.Now,
                //Gates = _context.Gate
                //                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                //                .ToList()
            };

            if (type == "materials")
                return View("CreateMatirialPermint");

            // لاحقاً لو عندك زوار أو سيارات
            if (type == "visitors")
                return View("CreateVisitorPermint");

            if (type == "cars")
                return View("CreateCarPermint");

            return View("CreateMatirialPermint"); // افتراضي
           
            
            // عرض صفحة الإضافة على حسب نوع التصريح
            //switch (type?.ToLower())
            //{
            //    case "materials":
            //        ViewData["Title"] = "إضافة تصريح مواد / معدات";
            //        break;
            //    case "visitors":
            //        ViewData["Title"] = "إضافة تصريح زوار";
            //        break;
            //    case "cars":
            //        ViewData["Title"] = "إضافة تصريح سيارات";
            //        break;
            //    default:
            //        ViewData["Title"] = "إضافة تصريح عام";
            //        break;
            //}

            //return View(model);
        }

        // 📌 POST: Permit/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PermitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ////model.Gates = _context.Gates
                ////                      .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                ////                      .ToList();
                return View(model);
            }

            // تحويل الـ ViewModel → Entity
            //var permit = new Permit
            //{
            //    No = model.No,
            //    ReqDepartment = model.ReqDepartment,
            //    Classification = model.Classification,
            //    Type = model.Type,
            //    ReqDepApproval = model.ReqDepApproval,
            //    SecuDepApproval = model.SecuDepApproval,
            //    GateId = model.GateId,
            //    Status = model.Status,
            //    StatusDescription = model.StatusDescription,
            //    IsClosed = model.IsClosed,
            //    CloseOn = model.CloseOn,
            //    MoveFrom = model.MoveFrom,
            //    MoveTo = model.MoveTo,
            //    RequoidedAs = model.RequoidedAs,
            //    PhoneNo = model.PhoneNo,
            //    Date = model.Date,
            //    HourOfEntry = model.HourOfEntry,
            //    Cars = model.Cars?.ToList() ?? new(),
            //    Humans = model.Humans?.ToList() ?? new(),
            //    EquipmentsAndMatirials = model.EquipmentsAndMatirials?.ToList() ?? new()
            //};

            //_context.Permits.Add(permit);
            //_context.SaveChanges();

            return RedirectToAction("Index");
        }

        // 📌 GET: Permit/Index
        public IActionResult Index()
        {
            //var permits = _context.Permits
            //    .Select(p => new
            //    {
            //        p.No,
            //        p.Type,
            //        p.ReqDepartment,
            //        p.Date,
            //        p.StatusDescription
            //    })
            //    .ToList();

            // return View(permits);
            return View();
        }
    }
}
