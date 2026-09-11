using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;
using Microsoft.OpenApi.Validations;
using SmartRFQ.API.Data;
using SmartRFQ.API.DTOs;
using SmartRFQ.API.Services;

using System.Security.Claims;

namespace SmartRFQ.API.Controllers;

[ApiController]
[Route("api/doc-request")]
[Authorize]
public class DocRequestController(IDocRequestService svc, IEmailService emailSvc, AppDbContext db, IDocRequestService geminiSvc) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUserRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? "user";

    private const string ErrorIdDataKey = "ErrorId";

    private IActionResult ErrorResult(int statusCode, Exception ex)
    {
        var errorId = ex.Data.Contains(ErrorIdDataKey) ? ex.Data[ErrorIdDataKey]?.ToString() : null;

        if (statusCode >= 500)
        {
            return StatusCode(statusCode, new
            {
                message = ex.Message,
                errorId,
                hint = "เกิดข้อผิดพลาดที่ไม่คาดคิด กรุณาแจ้ง Error ID นี้ให้ Programmer"
            });
        }

        return StatusCode(statusCode, new { message = ex.Message, errorId });
    }


    // GET /api/doc-request/{id}
   [HttpGet("{id:int}")]
public async Task<IActionResult> GetById(int id)
{
    try
    {
        var thaiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

        var doc = await db.DocRequests
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.RfqNo,
                d.RevNo,
                d.Status,
                d.ActualPurReplyDate,
                d.RejectReason,
                d.CreatedAt,
                d.UpdatedAt,
                d.RequesterId,
                RequesterEmail = d.Requester.Email,
                PurchaserEmail = d.Purchaser != null ? d.Purchaser.Email : null,
                Items = d.Items.OrderBy(i => i.Id).Select(i => new
                {
                    i.Id,
                    i.ProjectName,
                    i.GlCode,
                    i.SapItem,
                    i.ItemDescription,
                    i.SpecPartNo,
                    i.Model,
                    i.Brand,
                    i.ForGas,
                    i.Type,
                    i.SpecPurity,
                    i.CylinderType,
                    i.Quantity,
                    i.Uom,
                    i.CylinderSize,
                    i.MakerSource,
                    i.RequiredValve,
                    i.PurposeApplication,
                    i.Customer,
                    i.AddressLocation,
                    i.RecommendVendor,
                    i.Remark,
                    i.LeadTimeDays,
                    i.TargetPURreply,
                    i.AttachDwgPath,
                    i.AttachSpecPath,
                    i.AttachQuotationPath,
                    i.AttachEtcPath,
                    // ── EF.Property ใช้ได้ตรงนี้เพราะยังเป็น IQueryable
                    //    (ไม่มี .Include() นำหน้า ยังไม่ execute) ──
                    VendorQuotes = i.VendorQuotes.OrderBy(v => v.Id).Select(v => new
                    {
                        v.Id,
                        v.VendorName,
                        v.ItemDescription,
                        v.SpecPartNo,
                        v.Model,
                        v.BuyerEmail,
                        v.Remark,
                        v.Price,
                        v.Discount,
                        v.QuotationFilePath,
                        v.IsRecommended,
                        v.IsUserSelected,
                        v.UserDiffReason,
                        v.FinalPrice,
                        v.FinalDiscount,
                        v.FinalRemark,
                        v.FinalQuotationFilePath,
                        v.CostSavingReason,
                    })
                })
            })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (doc is null)
            return NotFound(new { message = "ไม่พบ RFQ" });

        if (doc.Status == "user_draft" && doc.RequesterId != CurrentUserId)
            return Forbid();

        return Ok(new
        {
            id = doc.Id,
            rfqNo = doc.RfqNo,
            revNo = doc.RevNo,
            status = doc.Status,
            actualPurReplyDate = doc.ActualPurReplyDate.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(doc.ActualPurReplyDate.Value, thaiZone).ToString("dd/MM/yy")
                : null,
            rejectReason = doc.RejectReason,
            createdAt = TimeZoneInfo.ConvertTimeFromUtc(doc.CreatedAt, thaiZone).ToString("dd/MM/yy HH:mm"),
            updatedAt = doc.UpdatedAt.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(doc.UpdatedAt.Value, thaiZone).ToString("dd/MM/yy HH:mm")
                : null,
            requesterEmail = doc.RequesterEmail,
            purchaserEmail = doc.PurchaserEmail,
            items = doc.Items.Select(i => new
            {
                i.Id,
                i.ProjectName,
                i.GlCode,
                i.SapItem,
                i.ItemDescription,
                i.SpecPartNo,
                i.Model,
                i.Brand,
                i.ForGas,
                i.Type,
                i.SpecPurity,
                i.CylinderType,
                i.Quantity,
                i.Uom,
                i.CylinderSize,
                i.MakerSource,
                i.RequiredValve,
                i.PurposeApplication,
                i.Customer,
                i.AddressLocation,
                i.RecommendVendor,
                i.Remark,
                i.LeadTimeDays,
                targetPURreply = i.TargetPURreply.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(i.TargetPURreply.Value, thaiZone).ToString("dd/MM/yy")
                    : null,
                i.AttachDwgPath,
                i.AttachSpecPath,
                i.AttachQuotationPath,
                i.AttachEtcPath,
                vendorQuotes = i.VendorQuotes
            })
        });
    }
    catch (Exception ex)
    {
        return ErrorResult(500, ex);
    }
}
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
        catch (InvalidOperationException ex)
        {
            return ErrorResult(400, ex);
        }
        catch (Exception ex)
        {
            return ErrorResult(500, ex);
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
            return ErrorResult(500, ex);
        }
    }

    // POST /api/doc-request/save-draft
    [HttpPost("save-draft")]
    public async Task<IActionResult> SaveDraft([FromForm] List<SaveDraftItemDto> items)
    {
        try
        {
            var result = await svc.CreateDraftAsync(items, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return ErrorResult(500, ex);
        }
    }


    // PUT /api/doc-request/{id}/save-draft
    [HttpPut("{id}/save-draft")]
    public async Task<IActionResult> UpdateDraft(int id, [FromForm] List<SaveDraftItemDto> items)
    {
        try
        {
            await svc.UpsertDraftItemsAsync(id, CurrentUserId, items);
            return Ok(new { message = "อัปเดต Draft สำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitDraft(int id, [FromForm] List<SaveDraftItemDto> items)
    {
        try
        {
            var rfqNo = await svc.SubmitDraftAsync(id, CurrentUserId, items);
            return Ok(new { rfqNo, message = "ส่ง RFQ สำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

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
            return ErrorResult(404, ex);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResult(400, ex);
        }
        catch (Exception ex)
        {
            return ErrorResult(500, ex);
        }
    }
    [HttpPost("{id}/items/{itemId}/quotation-pdf")]
    public async Task<IActionResult> UploadItemQuotationPdf(int id, int itemId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "ไม่มีไฟล์แนบ" });

        try
        {
            var url = await svc.UploadItemQuotationPdfAsync(id, itemId, file);

            if (url is null)
                return BadRequest(new { message = "ประเภทไฟล์ไม่ถูกต้อง หรืออัปโหลดไม่สำเร็จ" });

            return Ok(new { url });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/send-quotation-email")]
    public async Task<IActionResult> SendQuotation(int id, [FromBody] SendQuotationDto dto)
    {
        try
        {
            await svc.SendQuotationAsync(id, dto.Items, emailSvc);
            return Ok(new { message = "ส่ง email สำเร็จ และเปลี่ยนสถานะเป็น Waiting Quotation" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    // POST /api/doc-request/reject
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDocRequestDto dto)
    {
        try
        {
            var purchaserId = Guid.Parse(User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException());

            var (rfqNo, newRev, status, purchaserEmail) =
                await svc.RejectAsync(id, purchaserId, dto.Reason);
            return Ok(new { message = "Rejected", rfqNo, newRev, status, purchaserEmail });
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResult(404, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorResult(403, ex);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResult(400, ex);
        }
        catch (Exception ex)
        {
            return ErrorResult(500, ex);
        }
    }

    // POST /api/doc-request/cancel
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelDocRequestDto dto)
    {
        try
        {
            var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
            var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "user";

            var (rfqNo, purchaserId) = await svc.CancelAsync(id, actorId, actorRole, dto?.Reason);
            return Ok(new { message = "Cancelled", rfqNo, purchaserId });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPut("{id}/update")]
    public async Task<IActionResult> Update(int id, [FromForm] List<UpdateDocRequestItemDto> items)
    {
        try
        {
            var requesterId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

            var rfqNo = await svc.UpdateAndResubmitAsync(id, requesterId, items);
            return Ok(new { message = "Updated", rfqNo });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/start-compare")]
    public async Task<IActionResult> StartCompare(int id)
    {
        try
        {
            await svc.StartCompareAsync(id, CurrentUserId);
            return Ok(new { message = "เข้าสู่ Purchase Compare แล้ว" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPut("{id}/items/{itemId}/vendor-quotes")]
    public async Task<IActionResult> SaveVendorQuotes(int id, int itemId, [FromForm] List<VendorQuoteDto> quotes)

    {
        try
        {
            await svc.SaveVendorQuotesAsync(id, itemId, CurrentUserId, quotes);
            return Ok(new { message = "บันทึก Vendor Quotes สำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/finish-compare")]
    public async Task<IActionResult> FinishCompare(int id)
    {
        try
        {
            var rfqNo = await svc.FinishCompareAsync(id, CurrentUserId);
            return Ok(new { rfqNo, message = "Compare เสร็จสิ้น ส่งต่อ User Confirm" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/items/{itemId}/vendor-quotes/ai-extract")]
    public async Task<IActionResult> AiExtractVendorQuote(

    IFormFile? quotationFile,
    [FromForm] string? existingQuotationFilePath)
    {
        if (quotationFile == null && string.IsNullOrEmpty(existingQuotationFilePath))
            return BadRequest(new { message = "ไม่มีไฟล์แนบ" });

        try
        {
            VendorQuoteAiExtractResultDto result;

            if (quotationFile != null)
            {
                if (Path.GetExtension(quotationFile.FileName).ToLowerInvariant() != ".pdf")
                    return BadRequest(new { message = "AI Extract รองรับเฉพาะไฟล์ PDF" });
                result = await geminiSvc.ExtractQuotationDataAsync(quotationFile);
            }
            else
            {
                result = await ExtractQuotationDataFromUrlAsync(existingQuotationFilePath!);
            }

            if (!result.Success)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(result);
        }
        catch (Exception ex)
        {
            // ExtractQuotationDataAsync ปกติจะดักจับ error ภายในแล้ว return Success=false เอง
            // จุดนี้ดักไว้เผื่อ error ที่เกิดนอกเหนือ (เช่น ExtractQuotationDataFromUrlAsync ดาวน์โหลดไฟล์พัง)
            return ErrorResult(500, ex);
        }
    }

    [HttpPost("{id}/items/{itemId}/vendor-quotes/ai-hint")]
    public async Task<IActionResult> AiHintVendorQuotes(
        int id, int itemId,
        [FromForm] List<VendorHintFileDto> vendors)
    {
        if (vendors is null || vendors.Count == 0)
            return BadRequest(new { message = "ไม่มีข้อมูล Vendor ให้วิเคราะห์" });

        try
        {
            var result = await geminiSvc.GenerateVendorHintAsync(vendors);

            if (!result.Success)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ErrorResult(500, ex);
        }
    }

    private async Task<VendorQuoteAiExtractResultDto> ExtractQuotationDataFromUrlAsync(string fileUrl)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(fileUrl);

        if (!response.IsSuccessStatusCode)
        {
            return new VendorQuoteAiExtractResultDto
            {
                Success = false,
                ErrorMessage = "ไม่สามารถดาวน์โหลดไฟล์จาก URL ได้"
            };
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "quotation.pdf";

        var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "quotationFile", fileName)
        {
            ContentType = "application/pdf"
        };

        return await geminiSvc.ExtractQuotationDataAsync(formFile);
    }

    [HttpPut("{id}/items/{itemId}/user-choose")]
    public async Task<IActionResult> ChooseVendor(int id, int itemId, [FromBody] UserChooseVendorDto dto)
    {
        try
        {
            await svc.SaveUserChoiceAsync(id, itemId, CurrentUserId, dto.VendorQuoteId, dto.Reason);
            return Ok(new { message = "บันทึกตัวเลือกสำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/user-confirm")]
    public async Task<IActionResult> UserConfirm(int id, [FromBody] UserConfirmPayloadDto payload)
    {
        try
        {
            var rfqNo = await svc.UserConfirmAsync(id, CurrentUserId, payload.Items);
            return Ok(new { rfqNo, message = "ยืนยันสำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (UnauthorizedAccessException ex) { return ErrorResult(403, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPut("{id}/items/{itemId}/cost-saving")]
    public async Task<IActionResult> SaveCostSaving(int id, int itemId, [FromForm] CostSavingItemDto dto)
    {
        try
        {
            await svc.SaveCostSavingAsync(id, itemId, CurrentUserId, dto);
            return Ok(new { message = "บันทึก Cost Saving สำเร็จ" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/cost-saving/confirm")]
    public async Task<IActionResult> ConfirmCostSaving(int id)
    {
        try
        {
            var rfqNo = await svc.ConfirmCostSavingAsync(id, CurrentUserId);
            return Ok(new { rfqNo, message = "ส่งต่อ Purchase Confirm แล้ว" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/cost-saving/back-compare")]
    public async Task<IActionResult> BackToCompare(int id)
    {
        try
        {
            var rfqNo = await svc.BackToCompareAsync(id, CurrentUserId);
            return Ok(new { rfqNo, message = "ย้อนกลับไป Purchase Compare แล้ว" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }

    [HttpPost("{id}/items/{itemId}/purchase-confirm/approve")]
    public async Task<IActionResult> ApprovePurchaseConfirm(int id, int itemId)
    {
        try
        {
            var rfqNo = await svc.ApprovePurchaseConfirmAsync(id, itemId, CurrentUserId);
            return Ok(new { rfqNo, message = "อนุมัติและปิดรายการ (Finish) แล้ว" });
        }
        catch (KeyNotFoundException ex) { return ErrorResult(404, ex); }
        catch (InvalidOperationException ex) { return ErrorResult(400, ex); }
        catch (Exception ex) { return ErrorResult(500, ex); }
    }
}