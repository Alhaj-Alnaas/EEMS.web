using Core.Entities;
using Core.Interfaces.Services;
using DataAccess;
using PMS.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services;

namespace PMS.web.Controllers
{
    public class GateController : Controller
    {
        private readonly IGates _gateService;

        public GateController(IGates gateService)
        {
            _gateService = gateService;
        }

        // GET: GateController
        public async Task<IActionResult> Index()
        {
            var gates = await _gateService.GetAllAsync();
            return View(gates);
        }

        // GET: GateController/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            if (id == Guid.Empty)
                return NotFound();

            var gate = await _gateService.GetByIdAsync(id);

            if (gate == null)
                return NotFound();

            return View(gate);
        }

        // GET: GateController/Create
        public async Task<IActionResult> Create()
        {
            return View(new CreateGateViewModel());
        }

        // POST: GateController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGateViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);
            string userId = HttpContext.Session.GetString("UserName");
            var gate = new Gate
            {
                Id = Guid.NewGuid(),
                createdOn = DateTime.Now,
                createdBy = userId,
                updatedOn = DateTime.Now,
                updatedBy = userId,
                deletedBy = userId,
                isDeleted = false,
                no = vm.No,
                description = vm.Description,
                isActive = vm.IsActive,
                remarks = vm.Remarks
            };

            await _gateService.InsertAsync(gate);
            return RedirectToAction(nameof(Index));
        }

        // GET: GateController/Edit/5
        public async Task<IActionResult> Edit(Guid id)
        {
            var gate = await _gateService.GetByIdAsync(id);
            if (gate == null) return NotFound();

            var vm = new EditGateViewModel
            {
                Id = gate.Id,
                No = gate.no,
                Description = gate.description,
                IsActive = gate.isActive,
                Remarks = gate.remarks
            };

            return View(vm);
        }

        // POST: GateController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditGateViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var gate = await _gateService.GetByIdAsync(vm.Id);
            if (gate == null) return NotFound();

             gate = new Gate
            {
                Id = vm.Id,
                no = vm.No,
                description = vm.Description,
                isActive = vm.IsActive,
                remarks = vm.Remarks,
                updatedBy = "10067"
            };

            await _gateService.UpdateAsync(gate);

            return RedirectToAction(nameof(Index));
        }

        // GET: GateController/Delete/5
        public async Task<IActionResult> Delete(Guid id)
        {
            var gate = await _gateService.GetByIdAsync(id);
            if (gate == null) return NotFound();
            return View(gate);
        }

        // POST: GateController/Delete/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var gate = await _gateService.GetByIdAsync(id);
            if (gate == null)
                return NotFound();

            try
            {
                await _gateService.DeleteAsync(gate);
                TempData["SuccessMessage"] = "✅ تم حذف البوابة بنجاح.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));

        }
    }
}
