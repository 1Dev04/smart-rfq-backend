using SmartRFQ.API.Data;
using SmartRFQ.API.DTOs;
using SmartRFQ.API.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartRFQ.API.Services;

public interface IAuditLogService
{
    Task LogAsync(string rfqNo, string status, string role,
                  string eUser, string ePurchaser = "", string remark = "");

    Task<(IEnumerable<AuditLogResponseDto> data, int total)> GetAsync(AuditLogQueryDto query);
}

public class AuditLogService(AppDbContext db) : IAuditLogService
{

    public async Task LogAsync(
        string rfqNo,
        string status,
        string role,
        string eUser,
        string ePurchaser = "",
        string remark     = "")
    {
        db.AudioLogs.Add(new AuditLogs  
        {
            RfqNo       = rfqNo,
            Status      = status,
            Role        = role,
            E_User      = eUser,
            E_Purchaser = ePurchaser,
            Remark      = remark,
        });
        await db.SaveChangesAsync();
    }

    public async Task<(IEnumerable<AuditLogResponseDto> data, int total)> GetAsync(
        AuditLogQueryDto q)
    {
        
        var query = db.AudioLogs.AsQueryable();

        if (q.DateFrom.HasValue)
            query = query.Where(a => a.DateTime >= q.DateFrom);
        if (q.DateTo.HasValue)
            query = query.Where(a => a.DateTime < q.DateTo.Value.AddDays(1));
        if (!string.IsNullOrEmpty(q.E_User))
            query = query.Where(a => a.E_User.Contains(q.E_User));      
        if (!string.IsNullOrEmpty(q.E_Purchaser))
            query = query.Where(a => a.E_Purchaser.Contains(q.E_Purchaser)); 
        if (!string.IsNullOrEmpty(q.RfqNo))
            query = query.Where(a => a.RfqNo.Contains(q.RfqNo));
        if (!string.IsNullOrEmpty(q.Status))
            query = query.Where(a => a.Status == q.Status);              

        var total = await query.CountAsync();

        var data = await query
            .OrderByDescending(a => a.DateTime)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(a => new AuditLogResponseDto(  
                a.Id,
                a.RfqNo,
                a.Status,
                a.Role,
                a.E_User,
                a.E_Purchaser,
                a.Remark,
                a.DateTime.ToString("dd MMM yy, HH:mm")
            ))
            .ToListAsync();

        return (data, total);
    }
}