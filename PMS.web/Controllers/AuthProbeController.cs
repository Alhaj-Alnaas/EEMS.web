using Core.Interfaces.Services;
using Core.Interfaces.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Core.Entities;

namespace PMS.web.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthProbeController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IUserAccessService _userAccess;
        private readonly IGates _gates;

        public AuthProbeController(
            UserManager<User> userManager,
            IUserAccessService userAccess,
            IGates gates)
        {
            _userManager = userManager;
            _userAccess = userAccess;
            _gates = gates;
        }

        /// <summary>Used by the login screen to show gate selection only for gate officers.</summary>
        [HttpGet("gate-required")]
        public async Task<IActionResult> GateRequired([FromQuery] string? username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Ok(new { required = false, gates = Array.Empty<object>() });

            var user = await _userManager.FindByNameAsync(username.Trim());
            if (user == null)
                return Ok(new { required = false, gates = Array.Empty<object>() });

            var required = await _userAccess.IsGateOfficerAsync(user.Id)
                || string.Equals(user.UserType, "SaftyUser", StringComparison.OrdinalIgnoreCase);

            if (!required)
                return Ok(new { required = false, gates = Array.Empty<object>() });

            var gates = (await _gates.GetAllAsync())
                .Where(g => g.isActive && !g.isDeleted)
                .OrderBy(g => g.no)
                .Select(g => new
                {
                    id = g.Id.ToString(),
                    text = string.IsNullOrWhiteSpace(g.description) ? g.no : $"{g.no} — {g.description}"
                })
                .ToList();

            return Ok(new { required = true, gates });
        }
    }
}
