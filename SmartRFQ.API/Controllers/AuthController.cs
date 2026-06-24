
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartRFQ.API.DTOs;
using SmartRFQ.API.Services;

namespace SmartRFQ.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var (user, error) = await auth.LoginAsync(dto, Response);
        if (error is not null)
            return Unauthorized(new { message = error });
        return Ok(user);
    }
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var ok = await auth.RefreshAsync(Request, Response);
        return ok ? Ok() : Unauthorized(new { message = "Session expired." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await auth.RevokeAsync(Request, Response);
        return Ok(new
        {
            message = "Logged out.",
            clearStorage = new[] { "rfq_auth_token", "rfq_dark", "rfq_theme" }
        });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new AuthResponseDto(
        User.FindFirstValue(ClaimTypes.Name)!,
        User.FindFirstValue(ClaimTypes.Email)!,
        User.FindFirstValue(ClaimTypes.Role)!
    ));

    [ApiController]
    [Route("api/auditlog")]
    [Authorize(Roles = "admin,purchaser")]   
    public class AuditLogController(IAuditLogService auditSvc) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AuditLogQueryDto query)
        {
            var (data, total) = await auditSvc.GetAsync(query);
            return Ok(new { data, total });
        }
    }
}