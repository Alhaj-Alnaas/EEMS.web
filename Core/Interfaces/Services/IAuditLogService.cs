using Core.Entities;
using Core.Models;

namespace Core.Interfaces.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(AuditLogEntry entry);
        Task<List<AuditRow>> SearchAsync(AuditQuery query);
    }
}
