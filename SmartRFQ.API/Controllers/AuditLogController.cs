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
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "*" })]
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

    [HttpPost("seed")]
    public async Task<IActionResult> SeedData()
    {

        _db.AudioLogs.RemoveRange(_db.AudioLogs);
        await _db.SaveChangesAsync();


        var logs = new List<AuditLogs>
    {
        new() { RfqNo="RFQ-2606001", Status="Waiting", Role="user",     E_User="user.a@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-01 08:12:33") },
        new() { RfqNo="RFQ-2606001", Status="Accept",  Role="purchase", E_User="user.a@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Accepted by purchase",   DateTime=DateTime.Parse("2026-06-01 10:30:44") },
        new() { RfqNo="RFQ-2606002", Status="Waiting", Role="user",     E_User="user.b@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-02 08:44:00") },
        new() { RfqNo="RFQ-2606002", Status="Resent",  Role="purchase", E_User="user.b@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Resent for correction",  DateTime=DateTime.Parse("2026-06-02 09:15:22") },
        new() { RfqNo="RFQ-2606003", Status="Waiting", Role="user",     E_User="user.c@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-02 11:00:05") },
        new() { RfqNo="RFQ-2606003", Status="Cancel",  Role="user",     E_User="user.c@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Cancelled by requester", DateTime=DateTime.Parse("2026-06-02 13:45:18") },
        new() { RfqNo="RFQ-2606004", Status="Waiting", Role="user",     E_User="user.a@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-03 08:05:59") },
        new() { RfqNo="RFQ-2606004", Status="Accept",  Role="purchase", E_User="user.a@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Accepted",               DateTime=DateTime.Parse("2026-06-03 10:10:10") },
        new() { RfqNo="RFQ-2606005", Status="Waiting", Role="user",     E_User="user.d@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-03 11:30:00") },
        new() { RfqNo="RFQ-2606005", Status="Resent",  Role="purchase", E_User="user.d@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Missing attachment",     DateTime=DateTime.Parse("2026-06-03 14:00:00") },
        new() { RfqNo="RFQ-2606006", Status="Waiting", Role="user",     E_User="user.b@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-04 09:00:00") },
        new() { RfqNo="RFQ-2606006", Status="Cancel",  Role="user",     E_User="user.b@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Cancelled by requester", DateTime=DateTime.Parse("2026-06-04 11:00:00") },
        new() { RfqNo="RFQ-2606007", Status="Waiting", Role="user",     E_User="user.e@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-05 08:20:00") },
        new() { RfqNo="RFQ-2606007", Status="Accept",  Role="purchase", E_User="user.e@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Accepted",               DateTime=DateTime.Parse("2026-06-05 10:00:00") },
        new() { RfqNo="RFQ-2606008", Status="Waiting", Role="user",     E_User="user.c@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-05 13:10:00") },
        new() { RfqNo="RFQ-2606008", Status="Resent",  Role="purchase", E_User="user.c@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Incomplete spec",        DateTime=DateTime.Parse("2026-06-05 15:30:00") },
        new() { RfqNo="RFQ-2606009", Status="Waiting", Role="user",     E_User="user.a@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-06 08:00:00") },
        new() { RfqNo="RFQ-2606009", Status="Accept",  Role="purchase", E_User="user.a@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Accepted",               DateTime=DateTime.Parse("2026-06-06 10:30:00") },
        new() { RfqNo="RFQ-2606010", Status="Waiting", Role="user",     E_User="user.d@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-06 11:00:00") },
        new() { RfqNo="RFQ-2606010", Status="Cancel",  Role="user",     E_User="user.d@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Cancelled by requester", DateTime=DateTime.Parse("2026-06-06 13:00:00") },
        new() { RfqNo="RFQ-2606011", Status="Waiting", Role="user",     E_User="user.b@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-07 08:10:00") },
        new() { RfqNo="RFQ-2606011", Status="Accept",  Role="purchase", E_User="user.b@itg.co.th", E_Purchaser="purchase.c@itg.co.th", Remark="Accepted",               DateTime=DateTime.Parse("2026-06-07 09:30:00") },
        new() { RfqNo="RFQ-2606012", Status="Waiting", Role="user",     E_User="user.e@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-07 10:00:00") },
        new() { RfqNo="RFQ-2606012", Status="Resent",  Role="purchase", E_User="user.e@itg.co.th", E_Purchaser="purchase.a@itg.co.th", Remark="Wrong vendor info",      DateTime=DateTime.Parse("2026-06-07 11:30:00") },
        new() { RfqNo="RFQ-2606013", Status="Waiting", Role="user",     E_User="user.c@itg.co.th", E_Purchaser="purchase.b@itg.co.th", Remark="Created new RFQ",       DateTime=DateTime.Parse("2026-06-07 13:00:00") },
    };

        await _db.AudioLogs.AddRangeAsync(logs);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Seeded successfully", count = logs.Count });
    }


}

