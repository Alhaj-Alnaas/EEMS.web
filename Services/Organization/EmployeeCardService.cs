using Core.Entities;
using Core.Interfaces.Services.Organization;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services.Organization
{
    public class EmployeeCardService : IEmployeeCardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public EmployeeCardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<EmployeeCard> RecordPrintAsync(Guid employeeId, string? printedBy, CancellationToken ct = default)
        {
            var employee = await _unitOfWork.Employees.GetByIdAsync(employeeId)
                ?? throw new InvalidOperationException("الموظف غير موجود");

            var existing = await _unitOfWork.EmployeeCards.GetQueryable()
                .Where(c => c.EmployeeId == employeeId && !c.isDeleted && c.Status == "Active")
                .ToListAsync(ct);

            foreach (var card in existing)
            {
                card.Status = "Reprinted";
                card.updatedOn = DateTime.Now;
                _unitOfWork.EmployeeCards.Update(card);
            }

            var cardNumber = string.IsNullOrWhiteSpace(employee.CardNumber)
                ? employee.FileNumber
                : employee.CardNumber.Trim();

            var record = new EmployeeCard
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                CardNumber = cardNumber,
                PhotoPath = employee.PhotoPath,
                PrintedOn = DateTime.Now,
                PrintedBy = printedBy,
                Status = "Active",
                createdOn = DateTime.Now
            };

            _unitOfWork.EmployeeCards.Insert(record);
            await _unitOfWork.SaveAsync();
            return record;
        }

        public async Task<List<EmployeeCard>> GetByEmployeeAsync(Guid employeeId)
        {
            return await _unitOfWork.EmployeeCards.GetQueryable()
                .Where(c => c.EmployeeId == employeeId && !c.isDeleted)
                .OrderByDescending(c => c.PrintedOn)
                .ToListAsync();
        }
    }
}
