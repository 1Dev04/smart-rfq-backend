using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

using SmartRFQ.API.Data;
using SmartRFQ.API.Models;

namespace SmartRFQ.API.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditLogController(AppDbContext db)
    {
        _db = db;
    }


    [HttpGet]
    [EnableRateLimiting("audit")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetLogs([FromQuery] AuditLogQuery query)
    {
        var q = _db.AudioLogs
        .AsNoTracking()
        .AsQueryable();


        if (query.DateFrom.HasValue)
            q = q.Where(x => x.DateTime >= query.DateFrom.Value);
        if (query.DateTo.HasValue)
            q = q.Where(x => x.DateTime <= query.DateTo.Value);
        if (!string.IsNullOrEmpty(query.E_User))
            q = q.Where(x => x.E_User.Contains(query.E_User));
        if (!string.IsNullOrEmpty(query.E_Purchaser))
            q = q.Where(x => x.E_Purchaser.Contains(query.E_Purchaser));
        if (!string.IsNullOrEmpty(query.RfqNo))
            q = q.Where(x => x.RfqNo.Contains(query.RfqNo));
        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(x => x.Status == query.Status);

        if (query.PageSize > 100) query.PageSize = 100;

        var total = await q.CountAsync();
        var data = await q
            .OrderByDescending(x => x.DateTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.RfqNo,
                x.Status,
                x.Role,
                x.E_User,
                x.E_Purchaser,
                x.Remark,
                x.DateTime
            })
            .ToListAsync();

        return Ok(new { total, data });
    }

    // Lightweight summary for charting: returns daily counts in range
    [HttpGet("summary")]
    [EnableRateLimiting("audit")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetSummary([FromQuery] AuditLogQuery query)
    {
        var q = _db.AudioLogs.AsNoTracking().AsQueryable();

        if (query.DateFrom.HasValue)
            q = q.Where(x => x.DateTime >= query.DateFrom.Value);
        if (query.DateTo.HasValue)
            q = q.Where(x => x.DateTime <= query.DateTo.Value);
        if (!string.IsNullOrEmpty(query.E_User))
            q = q.Where(x => x.E_User.Contains(query.E_User));
        if (!string.IsNullOrEmpty(query.E_Purchaser))
            q = q.Where(x => x.E_Purchaser.Contains(query.E_Purchaser));
        if (!string.IsNullOrEmpty(query.RfqNo))
            q = q.Where(x => x.RfqNo.Contains(query.RfqNo));
        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(x => x.Status == query.Status);

        var grouped = await q
            .GroupBy(a => a.DateTime.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToListAsync();

        // return as { date: 'YYYY-MM-DD', count }
        var result = grouped.Select(g => new { date = g.Date.ToString("yyyy-MM-dd"), count = g.Count });
        return Ok(result);
    }

    
    

}

