using System;
using System.Threading.Tasks;
using API.Interface;
using API.Model;
using API.Service.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {

        private readonly IJWTManagerService _jWTManager;
        private readonly ReportAccessService _reportAccess;
        public AuthController(IJWTManagerService jWTManager, ReportAccessService reportAccess)
        {
            this._jWTManager = jWTManager;
            _reportAccess = reportAccess;
        }

        [Authorize]
        [HttpGet("permissions")]
        public async Task<IActionResult> Permissions()
        {
            var access = await _reportAccess.GetAsync(User);
            if (access == null) return Forbid();
            return Ok(new { isAdmin = access.IsAdmin, categories = access.Categories });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(User data)
        {
            try
            {
                var result = await _jWTManager.Authenticate(data);
                if (result != null)
                {
                    return Ok(result);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

    }
}
