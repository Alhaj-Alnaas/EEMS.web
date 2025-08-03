using Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IUserProvider
    {
        List<string> GetUserRolesAsync( User user);
        User GetCurrentUserAsync();
        string GetCurrentUserId();
        List<User> GetAllUsers(User CurrentUser);
        User GetUserById( string Id);
        User GetUserByFileNumber(string FileNumber);
        List<User> GetAllLeaders( User CurrentUser);
        User GetUserByResponsibilityCode( string ResponsibilityCode);

    }
}


