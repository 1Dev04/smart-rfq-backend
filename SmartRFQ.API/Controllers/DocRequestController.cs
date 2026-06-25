using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRFQ.API.DTOs;
using SmartRFQ.API.Services;
using System.Security.Claims;

namespace SmartRFQ.API.Controllers;

[ApiController]
[Route("api/doc-request")]
[Authorize]
public class DocRequestController(IDocRequestService svc) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUserRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? "user";

    // POST /api/doc-request/create
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromForm] List<CreateDocRequestItemDto> items)
    {
        if (items is null || items.Count == 0)
            return BadRequest(new { message = "ต้องมีอย่างน้อย 1 item" });
        if (items.Count > 50)
            return BadRequest(new { message = "ไม่เกิน 50 items ต่อ 1 RFQ" });

        try
        {
            var (id, rfqNo) = await svc.CreateAsync(items, CurrentUserId);
            return Ok(new { id, rfqNo, message = "สร้าง RFQ สำเร็จ" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    // GET /api/doc-request
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DocRequestQueryDto query)
    {
        try
        {
            var result = await svc.GetAllAsync(CurrentUserId, CurrentUserRole, query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    // POST /api/doc-request/accept
    // POST /api/doc-request/{id}/accept
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id, [FromBody] AcceptDocRequestDto dto)
    {
        try
        {

            var rfqNo = await svc.AcceptAsync(id, CurrentUserId, dto);
            return Ok(new { rfqNo, message = "RFQ accepted" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/doc-request/reject
    [HttpPost("reject")]
    public async Task<IActionResult> Reject([FromBody] DocRequestActionDto dto)
    {
        try
        {
            var (rfqNo, newRev) = await svc.RejectAsync(dto.Id);
            return Ok(new { message = "Rejected", rfqNo, newRev });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public record DocRequestActionDto(int Id);