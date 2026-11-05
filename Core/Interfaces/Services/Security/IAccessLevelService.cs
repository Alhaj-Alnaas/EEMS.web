using Core.Entities.Security;
using Core.Models.Security;

namespace Core.Interfaces.Services.Security
{
    public interface IAccessLevelService
    {
        Task<List<AccessLevel>> GetAllAsync(bool includeInactive = false);
        Task<AccessLevel?> GetByIdAsync(Guid id, bool includeTasks = true);
        Task<AccessLevel> CreateAsync(AccessLevelEditModel model, string? userName);
        Task UpdateAsync(AccessLevelEditModel model, string? userName);
        Task DeleteAsync(Guid id, string? userName);
        Task<bool> NameExistsAsync(string name, Guid? excludeId = null);
        Task<int> CountUsersAsync(Guid accessLevelId);
    }

    public interface ISystemTaskQueryService
    {
        Task<List<SystemTask>> GetAllActiveAsync();
        Task EnsureCatalogSeededAsync();
    }

    public interface IUserAccountService
    {
        Task<List<UserAccountListItem>> SearchAsync(string? fileNumber, int take = 200);
        Task<UserAccountDetail?> GetByFileNumberAsync(string fileNumber);
        Task<(bool Ok, string Message, string? UserId)> CreateAsync(CreateUserAccountModel model);
        Task<(bool Ok, string Message)> UpdateAccessLevelAsync(string userId, Guid accessLevelId);
        Task<(bool Ok, string Message, string? TempPassword)> ResetPasswordAsync(string userId);
        Task<(bool Ok, string Message)> DeleteAsync(string userId);
        Task<bool> ExistsByFileNumberAsync(string fileNumber);
    }

    public interface IUserAccessService
    {
        Task<IReadOnlySet<string>> GetTaskCodesForUserAsync(string userId);
        Task<bool> HasTaskAsync(string userId, string taskCode);
        Task<bool> IsGateOfficerAsync(string userId);
    }
}
