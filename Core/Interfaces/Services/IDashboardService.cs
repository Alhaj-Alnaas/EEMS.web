using Core.Entities;
using Core.Models;

namespace Core.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<LeaderDashboard> BuildAsync(User user);
    }
}
