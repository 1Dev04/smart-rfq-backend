using SmartRFQ.API.DTOs;
using SmartRFQ.API.Models;
using SmartRFQ.API.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartRFQ.API.Services;

public interface IDocRequestService
{
    Task<(int id, string rfqNo)> CreateAsync(List<CreateDocRequestItemDto> items, Guid requesterId);
    Task<DocRequestListResponseDto> GetAllAsync(Guid userId, string role, DocRequestQueryDto query);
    Task<string> AcceptAsync(int docRequestId, Guid purchaserId, AcceptDocRequestDto dto);

    Task<(string rfqNo, string newRev)> RejectAsync(int docRequestId);


}

public class DocRequestService(AppDbContext db, IWebHostEnvironment env) : IDocRequestService
{
    // ── Generate RFQ No ──────────────────────────────────────────
    private async Task<string> GenerateRfqNoAsync()
    {
        var prefix = $"RFQ-{DateTime.UtcNow:yyMM}";
        var count = await db.DocRequests.CountAsync(d => d.RfqNo.StartsWith(prefix));
        return $"{prefix}{(count + 1):D3}"; // RFQ-2606001
    }

    // ── Save File ────────────────────────────────────────────────
    private async Task<string?> SaveFileAsync(IFormFile? file, string folder)
    {
        if (file is null) return null;

        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".dwg", ".dxf" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return null;

        var dir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/uploads/{folder}/{fileName}";
    }

    // ── CREATE ───────────────────────────────────────────────────
    public async Task<(int id, string rfqNo)> CreateAsync(
        List<CreateDocRequestItemDto> items, Guid requesterId)
    {
        var doc = new DocRequest
        {
            RfqNo = await GenerateRfqNoAsync(),
            RevNo = "Rev.00",
            Status = "user_fill",
            RequesterId = requesterId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        foreach (var dto in items)
        {
            doc.Items.Add(new DocRequestItem
            {
                TargetPURreply = DateTime.TryParse(dto.TargetPURreply, out var d)
                                        ? d.ToUniversalTime() : null,
                ProjectName = dto.ProjectName.Trim(),
                GlCode = dto.GlCode.Trim(),
                SapItem = dto.SapItem?.Trim(),
                ItemDescription = dto.ItemDescription.Trim(),
                SpecPartNo = dto.SpecPartNo.Trim(),
                Model = dto.Model?.Trim(),
                Brand = dto.Brand.Trim(),
                ForGas = dto.ForGas.Trim(),
                Type = dto.Type.Trim(),
                SpecPurity = dto.SpecPurity.Trim(),
                CylinderType = dto.CylinderType.Trim(),
                Quantity = dto.Quantity,
                Uom = dto.Uom.Trim(),
                CylinderSize = dto.CylinderSize.Trim(),
                MakerSource = dto.MakerSource.Trim(),
                RequiredValve = dto.RequiredValve.Trim(),
                PurposeApplication = dto.PurposeApplication.Trim(),
                Customer = dto.Customer.Trim(),
                AddressLocation = dto.AddressLocation.Trim(),
                RecommendVendor = dto.RecommendVendor.Trim(),
                Remark = dto.Remark.Trim(),
                AttachDwgPath = await SaveFileAsync(dto.AttachDWG, "dwg"),
                AttachSpecPath = await SaveFileAsync(dto.AttachSpec, "spec"),
                AttachQuotationPath = await SaveFileAsync(dto.AttachQuotation, "quotation"),
                AttachEtcPath = await SaveFileAsync(dto.AttachEtc, "etc"),
                CreatedAt = DateTime.UtcNow,
            });
        }

        db.DocRequests.Add(doc);
        await db.SaveChangesAsync();

        return (doc.Id, doc.RfqNo);
    }

    // ── GET LIST ─────────────────────────────────────────────────
    public async Task<DocRequestListResponseDto> GetAllAsync(
        Guid userId, string role, DocRequestQueryDto query)
    {
        

        var q = db.DocRequests
            .Include(r => r.Requester)
            .Include(r => r.Purchaser)
            .Include(r => r.Items)
            .AsNoTracking()
            .AsQueryable();

        if (role == "user")
            q = q.Where(r => r.RequesterId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r =>
                r.RfqNo.Contains(s) ||
                r.Items.Any(i =>
                    i.ProjectName.Contains(s) ||
                    i.ItemDescription.Contains(s) ||
                    i.Customer.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(r => r.Status == query.Status);

        var total = await q.CountAsync();

        var docs = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var rows = new List<DocRequestListRowDto>();
        foreach (var doc in docs)
        {
            var itemList = doc.Items.OrderBy(i => i.Id).ToList();
            for (int idx = 0; idx < itemList.Count; idx++)
            {
                var item = itemList[idx];

                // ✅ ใช้ค่าที่ Purchase กำหนดไว้ — ลบการคำนวณเดิมออก
                int? leadDays = item.LeadTimeDays;

                rows.Add(new DocRequestListRowDto(
                    CreatedAt: doc.CreatedAt.ToString("dd/MM/yy"),
                    RfqNo: doc.RfqNo,
                    DocRequestId: doc.Id,
                    ItemType: item.Type,
                    ItemDescription: item.ItemDescription,
                    Quantity: item.Quantity,
                    Uom: item.Uom,
                    Status: doc.Status,
                    RequesterEmail: doc.Requester.Email,
                    PurchaserEmail: doc.Purchaser?.Email,
                    LeadTimeDays: leadDays,
                    IsFirstItemOfRfq: idx == 0
                ));
            }
        }

        return new DocRequestListResponseDto(rows, total, query.Page, query.PageSize);
    }
    // ── ACCEPT ───────────────────────────────────────────────────
    public async Task<string> AcceptAsync(int docRequestId, Guid purchaserId, AcceptDocRequestDto dto)
    {
        var doc = await db.DocRequests
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == docRequestId)
            ?? throw new KeyNotFoundException("ไม่พบ RFQ");

        if (doc.Status != "user_fill")
            throw new InvalidOperationException($"ไม่สามารถ Accept ได้ สถานะปัจจุบัน: {doc.Status}");

        doc.Status = "purchase_accept";
        doc.PurchaserId = purchaserId;
        doc.UpdatedAt = DateTime.UtcNow;

        foreach (var item in doc.Items)
        {
            if (dto.ItemLeadTimes.TryGetValue(item.Id, out var leadDays))
                item.LeadTimeDays = leadDays;   // ← Purchase กำหนด lead time ต่อ item

            item.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return doc.RfqNo;
    }
    // ── REJECT ───────────────────────────────────────────────────
    public async Task<(string rfqNo, string newRev)> RejectAsync(int docRequestId)
    {
        var doc = await db.DocRequests.FindAsync(docRequestId)
            ?? throw new KeyNotFoundException("ไม่พบ RFQ");

        var currentRev = int.TryParse(doc.RevNo.Replace("Rev.", ""), out var r) ? r : 0;
        doc.RevNo = $"Rev.{(currentRev + 1):D2}";
        doc.Status = "user_fill";
        doc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return (doc.RfqNo, doc.RevNo);
    }


}