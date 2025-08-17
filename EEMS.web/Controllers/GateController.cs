using Core.Entities;
using Core.Interfaces.Services;
using DataAccess;
using EEMS.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services;

namespace EEMS.web.Controllers
{
    public class GateController : Controller
    {
        private readonly IGates _gateService;
        private readonly IPermitType _permitTypeService;

        public GateController(IGates gateService, IPermitType permitTypeService)
        {
            _gateService = gateService;
            _permitTypeService = permitTypeService;
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
            var vm = new CreateGateViewModel
            {
                PermitTypes = (await _permitTypeService.GetAllAsync()).ToList(),
                SelectedPermitTypeIds = new List<int>()
            };
            return View(vm);
        }

        // POST: GateController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.PermitTypes = (await _permitTypeService.GetAllAsync()).ToList();
                return View(vm);
            }

            var gate = new Gate
            {
                Id = Guid.NewGuid(),
                createdOn = DateTime.Now,
                createdBy = "10067",
                updatedOn = DateTime.Now,
                updatedBy = "10067",
                deletedBy = "10067",
                isDeleted = false,
                no = vm.No,
                description = vm.Description,
                isActive = vm.IsActive,
                remarks = vm.Remarks
            };

            await _gateService.InsertAsync(gate, vm.SelectedPermitTypeIds);
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
                Remarks = gate.remarks,
                PermitTypes = (await _permitTypeService.GetAllAsync()).ToList(),
                SelectedPermitTypeIds = gate.PermitTypes.Select(p => p.Id).ToList()
            };

            return View(vm);
        }

        // POST: GateController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditGateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.PermitTypes = (await _permitTypeService.GetAllAsync()).ToList();
                return View(vm);
            }

            var gate = new Gate
            {
                Id = vm.Id,
                no = vm.No,
                description = vm.Description,
                isActive = vm.IsActive,
                remarks = vm.Remarks,
                updatedBy = "10067"
            };

            await _gateService.UpdateAsync(gate, vm.SelectedPermitTypeIds);
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

            await _gateService.DeleteAsync(gate);
   
            TempData["SuccessMessage"] = "✅ تم حذف البوابة بنجاح.";
            return RedirectToAction(nameof(Index));

        }
    }
}
