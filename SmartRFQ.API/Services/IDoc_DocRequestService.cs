using SmartRFQ.API.DTOs;
using SmartRFQ.API.Models;
using SmartRFQ.API.Data;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Azure.Core;
using System.Xml;
using Microsoft.AspNetCore.Components.Web;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.Identity.Client;
using CloudinaryDotNet.Core;
using System.Text.Json;
using System.Globalization;
using System.Text;
using Npgsql.Internal;


namespace SmartRFQ.API.Services;

public interface IDocRequestService
{

    Task<(int id, string rfqNo)> CreateAsync(List<CreateDocRequestItemDto> items, Guid requesterId);
    Task<DocRequestListResponseDto> GetAllAsync(Guid userId, string role, DocRequestQueryDto query);
    Task<string> AcceptAsync(int docRequestId, Guid purchaserId, AcceptDocRequestDto dto);

    Task<(string rfqNo, string newRev, string status, string? purchaserEmail)> RejectAsync(int docRequestId, Guid purchaserId, string? reason = null);
    Task<(string rfqNo, Guid? purchaserId)> CancelAsync(int docRequestId, Guid actorId, string actorRole, string? reason = null);

    Task SendQuotationAsync(int docRequestId, List<ItemVendorEmailDto> items, IEmailService emailSvc);

    Task<string> UpdateAndResubmitAsync(int docRequestId, Guid requesterId, List<UpdateDocRequestItemDto> items);

    Task SetItemQuotationPdfPathAsync(int itemId, string pdfPath);

    Task<UpsertDraftResultDto> CreateDraftAsync(List<SaveDraftItemDto> items, Guid userId);
    Task UpsertDraftItemsAsync(int docRequestId, Guid userId, List<SaveDraftItemDto> items);

    Task<string> SubmitDraftAsync(int docRequestId, Guid userId, List<SaveDraftItemDto> items);
    Task<string?> UploadItemQuotationPdfAsync(int id, int itemId, IFormFile file);

    Task StartCompareAsync(int docRequestId, Guid purchaserId);
    Task SaveVendorQuotesAsync(int docRequestId, int itemId, Guid purchaserId, List<VendorQuoteDto> quotes);

    Task<string> FinishCompareAsync(int docRequestId, Guid purchaserId);
    Task<VendorQuoteAiExtractResultDto> ExtractQuotationDataAsync(IFormFile pdfFile);
    Task<VendorQuoteAiHintResultDto> GenerateVendorHintAsync(List<VendorHintFileDto> vendorInputs);

    Task SaveUserChoiceAsync(int docRequestId, int itemId, Guid userId, int chosenVendorQuoteId, string? reason);
    Task<string> UserConfirmAsync(int docRequestId, Guid userId, List<UserConfirmItemDto> items);

    Task SaveCostSavingAsync(int docRequestId, int itemId, Guid purchaserId, CostSavingItemDto dto);
    Task<string> ConfirmCostSavingAsync(int docRequestId, Guid purchaserId);
    Task<string> BackToCompareAsync(int docRequestId, Guid purchaserId);
    Task<string> ApprovePurchaseConfirmAsync(int docRequestId, int itemId, Guid purchaserId);
}


public class DocRequestService : IDocRequestService
{
    private readonly AppDbContext db;
    private readonly IWebHostEnvironment env;
    private readonly Cloudinary _cloudinary;
    private readonly IBusinessDayCalculator _calc;
    private static readonly TimeZoneInfo ThaiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IAuditLogService _audit;


    public const string ErrorIdKey = "ErrorId";

    public DocRequestService(AppDbContext dbContext, IWebHostEnvironment environment,
         Cloudinary cloudinary, IBusinessDayCalculator calculator,
         IHttpClientFactory httpFactory, IConfiguration config,
         IAuditLogService audit)
    {
        db = dbContext;
        env = environment;
        _cloudinary = cloudinary;
        _calc = calculator;
        _httpFactory = httpFactory;
        _config = config;
        _audit = audit;
    }

    // ── Error Audit Helper ─────────────────────────────────────────
    public const string ErrorStatus = "error";
    private const int RemarkMaxLength = 255;

    private static string TruncateForRemark(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= RemarkMaxLength ? s : s[..RemarkMaxLength]);

    private async Task<string> LogOperationErrorAsync(
        Exception ex,
        string action,
        string? rfqNo,
        string? status,
        string role,
        Guid? actorId,
        Guid? targetId,
        string? extraContext = null)
    {
        var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        ex.Data[ErrorIdKey] = errorId;

        try
        {
            var actorEmail = await GetUserEmailAsync(actorId);
            var targetEmail = await GetUserEmailAsync(targetId);
            var contextPart = string.IsNullOrWhiteSpace(extraContext) ? "" : $" | Context: {extraContext}";
            var prevStatus = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
            var msg = $"[ERROR:{errorId}] เดิมสถานะ={prevStatus} | {action} ล้มเหลว: {ex.GetType().Name} - {ex.Message}{contextPart}";

            var safeRfqNo = string.IsNullOrWhiteSpace(rfqNo) ? "-" : rfqNo;

            await _audit.LogAsync(safeRfqNo, ErrorStatus, role, actorEmail, targetEmail, TruncateForRemark(msg));
        }
        catch
        {
            // ห้ามปล่อยให้การ log ผิดพลาด ไปบดบัง exception ตัวจริงของ operation เด็ดขาด
        }

        return errorId;
    }

    public async Task<string> SubmitDraftAsync(int docRequestId, Guid userId, List<SaveDraftItemDto> items)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.Id == docRequestId)
                    ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.RequesterId != userId) throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์ส่ง RFQ นี้");
            if (doc.Status != "user_draft") throw new InvalidOperationException($"ส่งไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            await UpsertDraftItemFieldsAsync(doc, items);

            doc.Status = "user_pending";
            doc.UpdatedAt = DateTime.UtcNow;

            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(userId), await GetUserEmailAsync(doc.PurchaserId), "User confirmed selection"); } catch { }

            await db.SaveChangesAsync();

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SubmitDraftAsync), doc?.RfqNo, doc?.Status, "user", userId, doc?.PurchaserId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    private async Task<string> GetUserEmailAsync(Guid? id)
    {
        if (!id.HasValue) return string.Empty;
        var u = await db.Users.FindAsync(id.Value);
        return u?.Email ?? string.Empty;
    }

    private static decimal? ParseDecimalFromJson(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            try { return el.GetDecimal(); } catch { return null; }
        }
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d)) return d;
        }
        return null;
    }

    public async Task<VendorQuoteAiExtractResultDto> ExtractQuotationDataAsync(IFormFile pdfFile)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-3.5-flash-lite";

        if (string.IsNullOrEmpty(apiKey))
            return new() { Success = false, ErrorMessage = "ยังไม่ได้ตั้งค่า Gemini API Key" };

        using var ms = new MemoryStream();
        await pdfFile.CopyToAsync(ms);
        var base64Pdf = Convert.ToBase64String(ms.ToArray());

        var prompt = """
คุณเป็นผู้เชี่ยวชาญด้านการตรวจสอบเอกสารใบเสนอราคา (Quotation) จาก Vendor
เอกสารอาจเป็นภาษาไทยหรือภาษาอังกฤษ (หรือผสมทั้งสองภาษาในไฟล์เดียวกัน) — อ่านและดึงข้อมูลได้ทั้งสองภาษาตามที่ปรากฏจริง
อ่านไฟล์ PDF ที่แนบมาอย่างละเอียด ทั้งข้อความและรูปภาพ/ตารางที่ปรากฏในเอกสาร
แล้วดึงข้อมูลต่อไปนี้ออกมาเป็น JSON เท่านั้น ห้ามมีข้อความอื่นนอกเหนือ JSON:

- vendorName: ชื่อบริษัท Vendor ที่ออกใบเสนอราคานี้ (ดูจากหัวกระดาษ/โลโก้/ลายเซ็นบริษัท) — คงชื่อไว้ตามที่ปรากฏในเอกสาร ไม่ต้องแปล
- itemDescription: รายละเอียดสินค้า/บริการหลักที่เสนอราคา (ถ้ามีหลายรายการ ให้สรุปรวมเป็นข้อความเดียว คั่นด้วย ", ") — เขียนตามภาษาต้นฉบับในเอกสาร ไม่ต้องแปล
- specPartNo: Specification หรือ Part Number ถ้ามีระบุ
- model: รุ่นสินค้า ถ้ามีระบุ
- price: ราคารวมสุทธิหลังหักส่วนลด รวม VAT แล้ว (grand total / ยอดสุทธิ) เป็นตัวเลขเท่านั้น ไม่มีสัญลักษณ์สกุลเงิน, คอมม่า, หรือช่องว่าง — ถ้าเอกสารมีหลายยอด (subtotal, VAT, grand total) ให้เลือกยอดสุดท้ายที่มากที่สุดเสมอ
- remark: หมายเหตุสำคัญอื่นๆ เช่น เงื่อนไขการชำระเงิน, ระยะเวลาส่งมอบ, วันหมดอายุใบเสนอราคา — เขียนตามภาษาต้นฉบับ ถ้ามีหลายรายการ ให้คั่นแต่ละรายการด้วย ", " (ห้ามใช้ ";" หรือ "; " เป็นตัวคั่นเด็ดขาด)

กฎสำคัญ:
- อ่านค่าตามที่ปรากฏในเอกสารจริง ห้ามแปลภาษาหรือเดาค่าที่ไม่มีในเอกสาร
- ถ้าข้อมูลใดไม่พบในเอกสาร ให้ใส่ค่าเป็น null สำหรับ field นั้น (ห้ามใส่ข้อความอธิบายแทน)
- price ต้องเป็นตัวเลขล้วน (number) ไม่ใช่ string
- ทุก field ที่เป็นข้อความ (string) ห้ามใช้เครื่องหมาย ";" หรือ "; " เป็นตัวคั่นรายการเด็ดขาด ให้ใช้ ", " เท่านั้น
""";
        var requestBody = new
        {
            contents = new[]
                   {
                new
                {
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = "application/pdf", data = base64Pdf } },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        vendorName = new { type = "STRING" },
                        itemDescription = new { type = "STRING" },
                        specPartNo = new { type = "STRING" },
                        model = new { type = "STRING" },
                        price = new { type = "NUMBER" },
                        remark = new { type = "STRING" }
                    }
                }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var http = _httpFactory.CreateClient("Gemini");
            var res = await http.PostAsync(url, content);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync();
                var errorId1 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId1}] ExtractQuotationDataAsync: Gemini API error {res.StatusCode} — {errBody}")); } catch { }
                return new() { Success = false, ErrorMessage = $"Gemini API error: {res.StatusCode} — {errBody} (Error ID: {errorId1}, กรุณาแจ้ง Programmer)" };
            }

            var resBody = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resBody);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var extracted = JsonSerializer.Deserialize<JsonElement>(text!);

            var result = new VendorQuoteAiExtractResultDto
            {
                Success = true,
                VendorName = extracted.TryGetProperty("vendorName", out var vn) ? vn.GetString() : null,
                ItemDescription = extracted.TryGetProperty("itemDescription", out var id) ? id.GetString() : null,
                SpecPartNo = extracted.TryGetProperty("specPartNo", out var sp) ? sp.GetString() : null,
                Model = extracted.TryGetProperty("model", out var mo) ? mo.GetString() : null,
                Price = extracted.TryGetProperty("price", out var pr) ? ParseDecimalFromJson(pr) : null,
                Remark = extracted.TryGetProperty("remark", out var rm) ? rm.GetString() : null,
            };

            // Retry with a focused prompt if price or description missing
            var needRetry = result.Price == null || string.IsNullOrWhiteSpace(result.ItemDescription);
            if (needRetry)
            {
                var retryPrompt = "คุณเป็นผู้เชี่ยวชาญในการค้นหายอดรวมในเอกสารใบเสนอราคา (Quotation). อ่านไฟล์ PDF ที่แนบแล้วหาค่า 'grand total', 'total', 'ยอดสุทธิ', 'Total Amount' หรือยอดเงินสุทธิที่เป็นตัวเลขจำนวนเต็ม/ทศนิยม หากพบหลายตัว ให้เลือกยอดที่มากที่สุด. นอกจากนี้ ถ้าสามารถสกัดคำอธิบายรายการหลัก (itemDescription) ได้ ให้คืนด้วย. ส่งผลลัพธ์เป็น JSON เท่านั้น: { \"price\": number|null, \"itemDescription\": string|null }";

                var retryBody = new
                {
                    contents = new[]
                   {
                        new
                        {
                            parts = new object[]
                            {
                                new { inline_data = new { mime_type = "application/pdf", data = base64Pdf } },
                                new { text = retryPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        response_mime_type = "application/json",
                        response_schema = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                price = new { type = "NUMBER" },
                                itemDescription = new { type = "STRING" }
                            }
                        }
                    }
                };

                var retryJson = JsonSerializer.Serialize(retryBody);
                var retryContent = new StringContent(retryJson, Encoding.UTF8, "application/json");
                var retryRes = await http.PostAsync(url, retryContent);
                if (retryRes.IsSuccessStatusCode)
                {
                    var retryResBody = await retryRes.Content.ReadAsStringAsync();
                    using var retryDoc = JsonDocument.Parse(retryResBody);
                    var retryText = retryDoc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    var retryExtract = JsonSerializer.Deserialize<JsonElement>(retryText!);
                    if (retryExtract.TryGetProperty("price", out var rpr))
                        result.Price = ParseDecimalFromJson(rpr);
                    if (retryExtract.TryGetProperty("itemDescription", out var rid) && rid.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(result.ItemDescription))
                        result.ItemDescription = rid.GetString();
                }
            }

            return result;

        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            var errorId2 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId2}] ExtractQuotationDataAsync timeout: {ex.Message}")); } catch { }
            return new() { Success = false, ErrorMessage = $"Gemini ใช้เวลาวิเคราะห์นานเกินไป (timeout) กรุณาลองใหม่อีกครั้ง (Error ID: {errorId2}, ถ้ายังไม่หาย กรุณาแจ้ง Programmer)" };
        }
        catch (Exception ex)
        {
            var errorId3 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId3}] ExtractQuotationDataAsync: {ex.GetType().Name} - {ex.Message}")); } catch { }
            return new() { Success = false, ErrorMessage = $"อ่านไฟล์ไม่สำเร็จ: {ex.Message} (Error ID: {errorId3}, กรุณาแจ้ง Programmer)" };
        }
    }


    public async Task SendQuotationAsync(int docRequestId, List<ItemVendorEmailDto> items, IEmailService emailSvc)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests
                .Include(d => d.Items)
                .Include(d => d.Requester)
                .Include(d => d.Purchaser)
                .FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "waiting_quotation")
                throw new InvalidOperationException($"ไม่สามารถส่ง Quotation ได้ สถานะปัจจุบัน: {doc.Status}");

            // ── เก็บ vendorName คู่กับ email + itemIds ที่เกี่ยวข้อง ──
            var vendorToItems = new Dictionary<string, (string? VendorName, List<int> ItemIds)>();
            foreach (var row in items)
                foreach (var recipient in row.Recipients.DistinctBy(r => r.Email))
                {
                    if (!vendorToItems.TryGetValue(recipient.Email, out var entry))
                    {
                        entry = (recipient.VendorName, new List<int>());
                        vendorToItems[recipient.Email] = entry;
                    }
                    if (!entry.ItemIds.Contains(row.ItemId)) entry.ItemIds.Add(row.ItemId);
                }

            // ── PrintedPdfUrl ต่อ item — เก็บไว้ map เพื่อดึงใช้ตอนสร้าง attachments ──
            var printedPdfByItem = items.ToDictionary(r => r.ItemId, r => r.PrintedPdfUrl);

            if (vendorToItems.Count > 0)
            {
                var requesterEmail = doc.Requester?.Email ?? throw new InvalidOperationException("ไม่พบอีเมล Requester");
                var purchaserEmail = doc.Purchaser?.Email ?? throw new InvalidOperationException("ไม่พบอีเมล Purchaser");

                foreach (var (vendorEmail, entry) in vendorToItems)
                {
                    var relevantItems = doc.Items.Where(i => entry.ItemIds.Contains(i.Id)).ToList();

                    // ── สร้างไฟล์แนบแยกกันเป็นคนละก้อน ไม่รวมเป็น PDF เดียว ──
                    var attachments = new List<EmailAttachmentRef>();

                    foreach (var item in relevantItems)
                    {
                        // หน้าสรุป (PDF ที่ frontend generate จาก Print Summary)
                        if (printedPdfByItem.TryGetValue(item.Id, out var printedUrl) && !string.IsNullOrWhiteSpace(printedUrl))
                            attachments.Add(new EmailAttachmentRef("Summary", printedUrl));

                        if (!string.IsNullOrWhiteSpace(item.AttachDwgPath))
                            attachments.Add(new EmailAttachmentRef("DWG", item.AttachDwgPath));

                        if (!string.IsNullOrWhiteSpace(item.AttachSpecPath))
                            attachments.Add(new EmailAttachmentRef("Spec", item.AttachSpecPath));

                        if (!string.IsNullOrWhiteSpace(item.AttachQuotationPath))
                            attachments.Add(new EmailAttachmentRef("LastQuotation", item.AttachQuotationPath));

                        if (!string.IsNullOrWhiteSpace(item.AttachEtcPath))
                            attachments.Add(new EmailAttachmentRef("ETC", item.AttachEtcPath));
                    }

                    await emailSvc.SendQuotationRequestAsync(
                        toEmail: vendorEmail,
                        vendorName: entry.VendorName,
                        rfqNo: doc.RfqNo,
                        requester: requesterEmail,
                        purchaser: purchaserEmail,
                        attachments: attachments);
                }
            }

            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(doc.PurchaserId), await GetUserEmailAsync(doc.RequesterId), "Send Quotation Requests"); } catch { }
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SendQuotationAsync), doc?.RfqNo, doc?.Status, "purchase", doc?.PurchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    
    private static DateTime? ParseThaiDateToUtc(string? raw)
    {
        if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
            return null;

        var thaiMidnight = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(thaiMidnight, ThaiZone);
    }

    private async Task<HashSet<DateOnly>> GetHolidaySetAsync()
    {
        var dates = await db.Holidays
            .Select(h => DateOnly.FromDateTime(h.CreatedAt))
            .ToListAsync();

        return dates.ToHashSet();
    }


    // ── Generate RFQ No ──────────────────────────────────────────

    private async Task<string> GenerateRfqNoAsync()
    {
        var prefix = $"RFQ-{DateTime.UtcNow:yyMM}";
        var count = await db.DocRequests.CountAsync(d => d.RfqNo.StartsWith(prefix));

        string rfqNo;
        do
        {
            count++;
            rfqNo = $"{prefix}{count:D3}";
        } while (await db.DocRequests.AnyAsync(d => d.RfqNo == rfqNo));

        return rfqNo;
    }

    // ── Save File ────────────────────────────────────────────────
    private async Task<string?> SaveFileAsync(IFormFile? file, string folder)
    {
        if (file is null) return null;

        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".dwg", ".dxf" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return null;

        await using var stream = file.OpenReadStream();

        string url;
        var publicId = Guid.NewGuid().ToString();

        try
        {
            if (ext is ".png" or ".jpg" or ".jpeg")
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"smart-rfq/{folder}",
                    PublicId = publicId
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null)
                    throw new Exception($"Cloudinary upload failed: {result.Error.Message}");
                url = result.SecureUrl.ToString();
            }
            else
            {
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"smart-rfq/{folder}",
                    PublicId = $"{publicId}{ext}",
                    UseFilename = false,
                    UniqueFilename = false
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null)
                    throw new Exception($"Cloudinary upload failed: {result.Error.Message}");
                url = result.SecureUrl.ToString();
            }
        }
        catch (Exception ex)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            ex.Data[ErrorIdKey] = errorId;
            try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId}] SaveFileAsync(folder={folder}, file={file.FileName}) ล้มเหลว: {ex.GetType().Name} - {ex.Message}")); } catch { }
            throw;
        }

        return url;
    }



    // ── quotation pdf ─────────────────────────────────────────────────
    public async Task<string?> UploadItemQuotationPdfAsync(int id, int itemId, IFormFile file)
    {
        try
        {
            var url = await SaveFileAsync(file, folder: $"quotation_img/{id}");
            if (url is null) return null;

            await SetItemQuotationPdfPathAsync(itemId, url);
            return url;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(UploadItemQuotationPdfAsync), null, "upload_quotation_pdf", "system", null, null, $"docRequestId={id}, itemId={itemId}");
            throw;
        }
    }


    // ── Normalize empty string to null ─────────────────────────────────────────────────
    private static string? Norm(string? s) =>
        string.IsNullOrEmpty(s) ? null : s;

    // ── CREATE ───────────────────────────────────────────────────
    public async Task<(int id, string rfqNo)> CreateAsync(
        List<CreateDocRequestItemDto> items, Guid requesterId)
    {
        var doc = new DocRequest
        {
            RfqNo = await GenerateRfqNoAsync(),
            RevNo = "Rev.00",
            Status = "user_pending",
            RequesterId = requesterId,
            CreatedAt = DateTime.UtcNow,

        };

        try
        {
            var holidaySet = await GetHolidaySetAsync();

            foreach (var dto in items)
            {
                var targetUtc = ParseThaiDateToUtc(dto.TargetPURreply);

                var leadTimeDays = _calc.CountBusinessDays(doc.CreatedAt, targetUtc, holidaySet);
                if (targetUtc is null || leadTimeDays <= 0)
                    throw new InvalidOperationException($"Target PUR Reply ไม่ถูกต้อง หรือไม่เหลือวันทำการให้ Purchase ตอบกลับ (Item: {dto.ItemDescription ?? dto.Type})");

                doc.Items.Add(new DocRequestItem
                {
                    TargetPURreply = targetUtc,
                    LeadTimeDays = leadTimeDays,


                    ProjectName = Norm(dto.ProjectName),
                    GlCode = Norm(dto.GlCode),
                    SapItem = Norm(dto.SapItem),
                    ItemDescription = Norm(dto.ItemDescription),
                    SpecPartNo = Norm(dto.SpecPartNo),
                    Model = Norm(dto.Model),
                    Brand = Norm(dto.Brand),
                    ForGas = Norm(dto.ForGas),

                    Type = dto.Type,

                    SpecPurity = Norm(dto.SpecPurity),
                    CylinderType = Norm(dto.CylinderType),
                    Quantity = dto.Quantity,
                    Uom = dto.Uom,
                    CylinderSize = Norm(dto.CylinderSize),
                    MakerSource = Norm(dto.MakerSource),
                    RequiredValve = Norm(dto.RequiredValve),
                    PurposeApplication = Norm(dto.PurposeApplication),
                    Customer = Norm(dto.Customer),
                    AddressLocation = Norm(dto.AddressLocation),
                    RecommendVendor = Norm(dto.RecommendVendor),
                    Remark = Norm(dto.Remark),

                    AttachDwgPath = await SaveFileAsync(dto.AttachDWG, "dwg"),
                    AttachSpecPath = await SaveFileAsync(dto.AttachSpec, "spec"),
                    AttachQuotationPath = await SaveFileAsync(dto.AttachQuotation, "quotation"),
                    AttachEtcPath = await SaveFileAsync(dto.AttachEtc, "etc"),
                    CreatedAt = DateTime.UtcNow,
                });
            }

            db.DocRequests.Add(doc);
            await db.SaveChangesAsync();

            // Audit: created by requester
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(requesterId), "", "Create RFQ"); } catch { }

            return (doc.Id, doc.RfqNo);
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(CreateAsync), doc.RfqNo, doc.Status, "user", requesterId, null);
            throw;
        }
    }


    // Create Draft ─────────────────────────────────────────────────
    public async Task<UpsertDraftResultDto> CreateDraftAsync(List<SaveDraftItemDto> items, Guid userId)
    {
        var doc = new DocRequest
        {
            RfqNo = await GenerateRfqNoAsync(),
            RevNo = "Rev.00",
            Status = "user_draft",
            RequesterId = userId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            db.DocRequests.Add(doc);

            await UpsertDraftItemFieldsAsync(doc, items);
            await db.SaveChangesAsync();
            // Audit: created draft by requester
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(userId), "", "Create Draft"); } catch { }

            return new UpsertDraftResultDto(doc.Id, doc.RfqNo);
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(CreateDraftAsync), doc.RfqNo, doc.Status, "user", userId, null);
            throw;
        }
    }

    // ── Update Draft ─────────────────────────────────────────────────
    public async Task UpsertDraftItemsAsync(int docRequestId, Guid userId, List<SaveDraftItemDto> items)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.RequesterId != userId)
                throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์แก้ไข Draft นี้");

            if (doc.Status != "user_draft")
                throw new InvalidOperationException($"แก้ไข Draft ไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            // Audit: update draft items
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(userId), await GetUserEmailAsync(doc.PurchaserId), "Update Draft Items"); } catch { }

            doc.UpdatedAt = DateTime.UtcNow;
            await UpsertDraftItemFieldsAsync(doc, items);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(UpsertDraftItemsAsync), doc?.RfqNo, doc?.Status, "user", userId, doc?.PurchaserId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    // ── Save Draft ─────────────────────────────────────────────────
    public async Task UpsertDraftItemFieldsAsync(DocRequest doc, List<SaveDraftItemDto> items)
    {

        var submittedIds = items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
        var toRemove = doc.Items.Where(existing => !submittedIds.Contains(existing.Id)).ToList();
        if (toRemove.Count > 0) db.DocRequestItems.RemoveRange(toRemove);

        var holidaySet = await GetHolidaySetAsync();

        foreach (var dto in items)

        {
            DateTime? targetUtc = null;
            int? leadTimeDays = null;

            if (!string.IsNullOrWhiteSpace(dto.TargetPURreply))
            {
                targetUtc = ParseThaiDateToUtc(dto.TargetPURreply);
                if (targetUtc is not null) leadTimeDays = _calc.CountBusinessDays(doc.CreatedAt, targetUtc, holidaySet);
            }
            // if (requireTarget && (targetUtc is null || leadTimeDays is null || leadTimeDays <= 0))
            //     throw new InvalidOperationException(
            //         $"Target PUR Reply ไม่ถูกต้อง หรือไม่เหลือวันทำการให้ Purchase ตอบกลับ (Item: {dto.ItemDescription ?? dto.Type})");
            if (targetUtc is null || leadTimeDays <= 0)
                throw new InvalidOperationException(
                    $"Target PUR Reply ไม่ถูกต้อง หรือไม่เหลือวันทำการให้ Purchase ตอบกลับ (Item: {dto.ItemDescription ?? dto.Type})");


            DocRequestItem item;
            if (dto.Id.HasValue && doc.Items.Any(x => x.Id == dto.Id.Value))
                item = doc.Items.First(x => x.Id == dto.Id.Value);
            else
            {
                item = new DocRequestItem { CreatedAt = DateTime.UtcNow };
                doc.Items.Add(item);
            }

            item.TargetPURreply = targetUtc;
            item.LeadTimeDays = leadTimeDays;
            item.ProjectName = Norm(dto.ProjectName);
            item.GlCode = Norm(dto.GlCode);
            item.SapItem = Norm(dto.SapItem);
            item.ItemDescription = Norm(dto.ItemDescription);
            item.SpecPartNo = Norm(dto.SpecPartNo);
            item.Model = Norm(dto.Model);
            item.Brand = Norm(dto.Brand);
            item.ForGas = Norm(dto.ForGas);
            item.Type = dto.Type;
            item.SpecPurity = Norm(dto.SpecPurity);
            item.CylinderType = Norm(dto.CylinderType);
            item.Quantity = dto.Quantity;
            item.Uom = dto.Uom;
            item.CylinderSize = Norm(dto.CylinderSize);
            item.MakerSource = Norm(dto.MakerSource);
            item.RequiredValve = Norm(dto.RequiredValve);
            item.PurposeApplication = Norm(dto.PurposeApplication);
            item.Customer = Norm(dto.Customer);
            item.AddressLocation = Norm(dto.AddressLocation);
            item.RecommendVendor = Norm(dto.RecommendVendor);
            item.Remark = Norm(dto.Remark);
            item.UpdatedAt = DateTime.UtcNow;

            if (dto.AttachDWG != null) item.AttachDwgPath = await SaveFileAsync(dto.AttachDWG, "dwg");
            else if (dto.RemoveAttachDWG) item.AttachDwgPath = null;

            if (dto.AttachSpec != null) item.AttachSpecPath = await SaveFileAsync(dto.AttachSpec, "spec");
            else if (dto.RemoveAttachSpec) item.AttachSpecPath = null;

            if (dto.AttachQuotation != null) item.AttachQuotationPath = await SaveFileAsync(dto.AttachQuotation, "quotation");
            else if (dto.RemoveAttachQuotation) item.AttachQuotationPath = null;

            if (dto.AttachEtc != null) item.AttachEtcPath = await SaveFileAsync(dto.AttachEtc, "etc");
            else if (dto.RemoveAttachEtc) item.AttachEtcPath = null;
        }


    }

    // ── Start Compare ─────────────────────────────────────────────────
    public async Task StartCompareAsync(int docRequestId, Guid purchaserId)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "waiting_quotation") throw new InvalidOperationException($"ไม่สามารถเข้าสู่ Compare ได้ สถานะปัจจุบัน: {doc.Status}");
            doc.Status = "purchase_compare";
            doc.ActualPurReplyDate = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: started compare
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), "Start compare"); } catch { }
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(StartCompareAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }



    // ── Save Vendor Quotes ─────────────────────────────────────────────────

    public async Task SaveVendorQuotesAsync(int docRequestId, int itemId, Guid purchaserId, List<VendorQuoteDto> quotes)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items).ThenInclude(i => i.VendorQuotes)
            .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "purchase_compare") throw new InvalidOperationException($"แก้ไข Compare ไม่ได้ สถานะปัจจุบัน: {doc.Status}");
            var item = doc.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new KeyNotFoundException("ไม่พบ Item");

            if (quotes.Count(q => q.IsRecommended) > 1) throw new InvalidOperationException("เลือก Recommended ได้เพียง 1 Vendor ต่อ Item");

            var submittedIds = quotes.Where(q => q.Id.HasValue).Select(q => q.Id!.Value).ToHashSet();
            var toRemove = item.VendorQuotes.Where(vq => !submittedIds.Contains(vq.Id)).ToList();
            if (toRemove.Count > 0) db.VendorQuotes.RemoveRange(toRemove);

            foreach (var dto in quotes)
            {
                VendorQuote vq;
                if (dto.Id.HasValue && item.VendorQuotes.Any(x => x.Id == dto.Id.Value))
                    vq = item.VendorQuotes.First(x => x.Id == dto.Id.Value);
                else
                {
                    vq = new VendorQuote { CreatedAt = DateTime.UtcNow };
                    item.VendorQuotes.Add(vq);
                }

                vq.VendorName = dto.VendorName;
                vq.ItemDescription = dto.ItemDescription;
                vq.SpecPartNo = dto.SpecPartNo;
                vq.Model = dto.Model;
                vq.BuyerEmail = dto.BuyerEmail;
                vq.Remark = dto.Remark;
                vq.Price = dto.Price;
                vq.Discount = dto.Discount;
                vq.IsRecommended = dto.IsRecommended;
                vq.UpdatedAt = DateTime.UtcNow;

                if (dto.QuotationFile != null)
                    vq.QuotationFilePath = await SaveFileAsync(dto.QuotationFile, "purchase_compare");
                else if (dto.RemoveQuotationFile)
                    vq.QuotationFilePath = null;
            }
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: vendor quotes saved
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), "Save vendor quotes"); } catch { }
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SaveVendorQuotesAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}, itemId={itemId}");
            throw;
        }
    }
    // ── Finish Compare ─────────────────────────────────────────────────

    public async Task<string> FinishCompareAsync(int docRequestId, Guid purchaserId)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items).ThenInclude(i => i.VendorQuotes)
                        .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "purchase_compare") throw new InvalidOperationException($"ไม่สามารถ Finish ได้ สถานะปัจจุบัน: {doc.Status}");
            var missing = doc.Items.Where(i => !i.VendorQuotes.Any(vq => vq.IsRecommended)).ToList();

            if (missing.Count > 0)
                throw new InvalidOperationException($"กรุณาเลือก Recommended ให้ครบทุก Item ก่อน Finish (ขาด {missing.Count} Item)");

            doc.Status = "user_confirm";
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: finished compare — moved to user_confirm
            try { await _audit.LogAsync(doc.RfqNo, "purchase_compare", "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), "Finish compare"); } catch { }

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(FinishCompareAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    // ── GET LIST ─────────────────────────────────────────────────
    public async Task<DocRequestListResponseDto> GetAllAsync(
        Guid userId, string role, DocRequestQueryDto query)
    {
        try
        {

            var q = db.DocRequests
          .Include(r => r.Requester)
          .Include(r => r.Purchaser)
          .Include(r => r.Items)
          .AsNoTracking()
          .AsQueryable();

            q = q.Where(r => r.Status != "user_draft" || r.RequesterId == userId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(r =>
        r.RfqNo.Contains(s) ||
        r.Items.Any(i =>
            (i.ProjectName != null && i.ProjectName.Contains(s)) ||
            (i.ItemDescription != null && i.ItemDescription.Contains(s)) ||
            (i.Customer != null && i.Customer.Contains(s))));
            }

            if (!string.IsNullOrWhiteSpace(query.ItemType))
            {
                var t = query.ItemType.Trim().ToLower();
                q = q.Where(r => r.Items.Any(i => i.Type != null && i.Type.ToLower().Contains(t)));
            }

            if (!string.IsNullOrWhiteSpace(query.ContactUser))
            {
                var cu = query.ContactUser.Trim().ToLower();
                q = q.Where(r => r.Requester != null && r.Requester.Email != null && r.Requester.Email.ToLower().Contains(cu));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(r => r.Status == query.Status);

            var total = await q.CountAsync();

            var docs = await q
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
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

                    // ใช้ค่าที่ Purchase กำหนดไว้ — ลบการคำนวณเดิมออก
                    int? leadDays = item.LeadTimeDays;
                    var thaiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

                    rows.Add(new DocRequestListRowDto(


                        CreatedAt: TimeZoneInfo.ConvertTimeFromUtc(doc.CreatedAt, thaiZone)
                               .ToString("dd/MM/yy HH:mm"),

                        UpdatedAt: doc.UpdatedAt.HasValue
        ? TimeZoneInfo.ConvertTimeFromUtc(doc.UpdatedAt.Value, thaiZone).ToString("dd/MM/yy HH:mm")
        : null,
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
                        IsFirstItemOfRfq: idx == 0,
                        TotalItems: doc.Items.Count,
                        WasRejected: doc.RejectedAt != null
                    ));
                }
            }

            var result = new DocRequestListResponseDto(rows, total, query.Page, query.PageSize);


            return result;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(GetAllAsync), null, "list_query", role, userId, null, $"search={query?.Search}, status={query?.Status}, page={query?.Page}");
            throw;
        }
    }

    // ── ACCEPT ───────────────────────────────────────────────────
    public async Task<string> AcceptAsync(int docRequestId, Guid purchaserId, AcceptDocRequestDto dto)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "user_pending")
                throw new InvalidOperationException($"ไม่สามารถ Accept ได้ สถานะปัจจุบัน: {doc.Status}");

            doc.Status = "waiting_quotation";
            doc.PurchaserId = purchaserId;
            doc.RejectedAt = null;
            doc.AcceptedAt = DateTime.UtcNow;
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
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(AcceptAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    // ── REJECT ───────────────────────────────────────────────────
    public async Task<(string rfqNo, string newRev, string status, string? purchaserEmail)> RejectAsync(int docRequestId, Guid purchaserId, string? reason = null)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests
                .Include(d => d.Purchaser)
                .FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            var currentRev = int.TryParse(doc.RevNo.Replace("Rev.", ""), out var r) ? r : 0;
            doc.RevNo = $"Rev.{(currentRev + 1):D2}";
            doc.Status = "reject";
            doc.PurchaserId = purchaserId;
            doc.RejectReason = reason;
            doc.RejectedAt = DateTime.UtcNow;

            doc.UpdatedAt = DateTime.UtcNow;


            await db.SaveChangesAsync();
            await db.Entry(doc).Reference(d => d.Purchaser).LoadAsync();

            // Audit: rejected by purchaser
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), string.IsNullOrWhiteSpace(reason) ? "Reject" : $"Reject: {reason}"); } catch { }

            return (doc.RfqNo, doc.RevNo, doc.Status, doc.Purchaser?.Email);
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(RejectAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}, reason={reason}");
            throw;
        }
    }

    // ── Cancel (by requester or purchaser) ──────────────────────────────────────
    public async Task<(string rfqNo, Guid? purchaserId)> CancelAsync(int docRequestId, Guid actorId, string actorRole, string? reason = null)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests
                .FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status == "finish")
                throw new InvalidOperationException($"ไม่สามารถยกเลิกได้ สถานะปัจจุบัน: {doc.Status}");

            // If actor is purchaser, allow cancel and set PurchaserId
            if (actorRole == "purchase")
            {
                doc.PurchaserId = actorId;
            }
            else
            {
                // otherwise require actor to be the original requester
                if (doc.RequesterId != actorId)
                    throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์ยกเลิก RFQ นี้");
            }

            doc.Status = "cancel";
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            // Audit: cancel by actor
            try
            {
                var role = actorRole == "purchase" ? "purchase" : "user";
                await _audit.LogAsync(doc.RfqNo, doc.Status, role, await GetUserEmailAsync(actorId), await GetUserEmailAsync(doc.PurchaserId), string.IsNullOrWhiteSpace(reason) ? "Cancel" : $"Cancel: {reason}");
            }
            catch { }

            return (doc.RfqNo, doc.PurchaserId);
        }
        catch (Exception ex)
        {
            var role = actorRole == "purchase" ? "purchase" : "user";
            await LogOperationErrorAsync(ex, nameof(CancelAsync), doc?.RfqNo, doc?.Status, role, actorId, doc?.PurchaserId, $"docRequestId={docRequestId}, reason={reason}");
            throw;
        }
    }

    // ── Update and ResubmitAsync ──────────────────────────────────
    public async Task<string> UpdateAndResubmitAsync(int docRequestId, Guid requesterId, List<UpdateDocRequestItemDto> items)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == docRequestId)
                ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.RequesterId != requesterId)
                throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์แก้ไข RFQ นี้");

            if (doc.Status != "reject")
                throw new InvalidOperationException($"ไม่สามารถแก้ไขได้ สถานะปัจจุบัน: {doc.Status}");

            var submittedIds = items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
            var toRemove = doc.Items.Where(existing => !submittedIds.Contains(existing.Id)).ToList();
            if (toRemove.Count > 0)
                db.DocRequestItems.RemoveRange(toRemove);


            var holidaySet = await GetHolidaySetAsync();

            foreach (var dto in items)
            {

                var targetUtc = ParseThaiDateToUtc(dto.TargetPURreply);
                var leadTimeDays = _calc.CountBusinessDays(doc.CreatedAt, targetUtc, holidaySet);
                if (targetUtc is null || leadTimeDays <= 0)
                    throw new InvalidOperationException(
                        $"Target PUR Reply ไม่ถูกต้อง หรือไม่เหลือวันทำการให้ Purchase ตอบกลับ (Item: {dto.ItemDescription ?? dto.Type})");

                DocRequestItem item;
                if (dto.Id.HasValue)
                {
                    item = doc.Items.First(x => x.Id == dto.Id.Value);
                }
                else
                {
                    item = new DocRequestItem { CreatedAt = DateTime.UtcNow };
                    doc.Items.Add(item);
                }

                // ── ใช้ค่าที่คำนวณแล้ว ไม่ parse ซ้ำด้วยวิธีอื่น ──
                item.TargetPURreply = targetUtc;
                item.LeadTimeDays = leadTimeDays;

                item.ProjectName = Norm(dto.ProjectName);
                item.GlCode = Norm(dto.GlCode);
                item.SapItem = Norm(dto.SapItem);
                item.ItemDescription = Norm(dto.ItemDescription);
                item.SpecPartNo = Norm(dto.SpecPartNo);
                item.Model = Norm(dto.Model);
                item.Brand = Norm(dto.Brand);
                item.ForGas = Norm(dto.ForGas);
                item.Type = dto.Type;
                item.SpecPurity = Norm(dto.SpecPurity);
                item.CylinderType = Norm(dto.CylinderType);
                item.Quantity = dto.Quantity;
                item.Uom = dto.Uom;
                item.CylinderSize = Norm(dto.CylinderSize);
                item.MakerSource = Norm(dto.MakerSource);
                item.RequiredValve = Norm(dto.RequiredValve);
                item.PurposeApplication = Norm(dto.PurposeApplication);
                item.Customer = Norm(dto.Customer);
                item.AddressLocation = Norm(dto.AddressLocation);
                item.RecommendVendor = Norm(dto.RecommendVendor);
                item.Remark = Norm(dto.Remark);
                item.UpdatedAt = DateTime.UtcNow;

                if (dto.AttachDWG != null)
                    item.AttachDwgPath = await SaveFileAsync(dto.AttachDWG, "dwg");
                else if (dto.RemoveAttachDWG)
                    item.AttachDwgPath = null;

                if (dto.AttachSpec != null)
                    item.AttachSpecPath = await SaveFileAsync(dto.AttachSpec, "spec");
                else if (dto.RemoveAttachSpec)
                    item.AttachSpecPath = null;

                if (dto.AttachQuotation != null)
                    item.AttachQuotationPath = await SaveFileAsync(dto.AttachQuotation, "quotation");
                else if (dto.RemoveAttachQuotation)
                    item.AttachQuotationPath = null;

                if (dto.AttachEtc != null)
                    item.AttachEtcPath = await SaveFileAsync(dto.AttachEtc, "etc");
                else if (dto.RemoveAttachEtc)
                    item.AttachEtcPath = null;
            }

            var currentRev = int.TryParse(doc.RevNo.Replace("Rev.", ""), out var r) ? r : 0;
            doc.RevNo = $"Rev.{(currentRev + 1):D2}";
            doc.RejectedAt = null;
            doc.RejectReason = null;
            doc.Status = "user_pending";
            doc.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // Audit: update and resubmit by requester
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(requesterId), await GetUserEmailAsync(doc.PurchaserId), "Update and resubmit"); } catch { }

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(UpdateAndResubmitAsync), doc?.RfqNo, doc?.Status, "user", requesterId, doc?.PurchaserId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    public async Task SetItemQuotationPdfPathAsync(int itemId, string pdfPath)
    {
        try
        {
            var item = await db.DocRequestItems.Include(i => i.DocRequest).FirstOrDefaultAsync(i => i.Id == itemId);
            if (item is null) throw new KeyNotFoundException($"ไม่พบ Item ID {itemId}");

            item.QuotationPdfPath = pdfPath;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SetItemQuotationPdfPathAsync), null, "set_quotation_pdf_path", "system", null, null, $"itemId={itemId}");
            throw;
        }
    }




    public async Task<VendorQuoteAiHintResultDto> GenerateVendorHintAsync(List<VendorHintFileDto> vendorInputs)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        if (string.IsNullOrEmpty(apiKey))
            return new() { Success = false, ErrorMessage = "ยังไม่ได้ตั้งค่า Gemini API Key" };

        // ── รวบรวม PDF ทั้ง 3 vendor เป็น base64 พร้อม index ──
        var parts = new List<object>();
        var foundAny = false;

        for (int i = 0; i < vendorInputs.Count; i++)
        {
            var idx = i + 1;
            var input = vendorInputs[i];
            byte[]? bytes = null;

            try
            {
                if (input.File != null)
                {
                    using var ms = new MemoryStream();
                    await input.File.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }
                else if (!string.IsNullOrWhiteSpace(input.ExistingFilePath))
                {
                    var http = _httpFactory.CreateClient("Gemini");
                    var res = await http.GetAsync(input.ExistingFilePath);
                    if (res.IsSuccessStatusCode)
                        bytes = await res.Content.ReadAsByteArrayAsync();
                }
            }
            catch
            {
                bytes = null; // ไฟล์อ่านไม่ได้ -> ถือว่าไม่มีข้อมูล ไม่ throw ทั้ง request
            }

            parts.Add(new { text = $"=== เอกสารของ Vendor {idx} (ชื่อที่ผู้ใช้กรอกไว้: {(string.IsNullOrWhiteSpace(input.VendorName) ? "ไม่ระบุ" : input.VendorName)}) ===" });

            if (bytes != null)
            {
                foundAny = true;
                parts.Add(new { inline_data = new { mime_type = "application/pdf", data = Convert.ToBase64String(bytes) } });
            }
            else
            {
                parts.Add(new { text = $"(Vendor {idx} ไม่มีไฟล์แนบ หรือดาวน์โหลดไฟล์ไม่สำเร็จ — ห้ามนำ Vendor นี้มาเทียบราคา ให้ระบุว่า \"ไม่มีข้อมูล\")" });
            }
        }

        if (!foundAny)
            return new() { Success = false, ErrorMessage = "ไม่มีไฟล์ใบเสนอราคาของ Vendor รายใดเลยให้วิเคราะห์" };

        var prompt = """
คุณเป็นนักจัดซื้ออาวุโส (Senior Purchaser) มีหน้าที่วิเคราะห์เปรียบเทียบใบเสนอราคา (Quotation) จาก Vendor สูงสุด 3 ราย
ที่แนบมาเป็นไฟล์ PDF ทีละราย (มีป้ายกำกับ "Vendor 1 / Vendor 2 / Vendor 3" นำหน้าแต่ละไฟล์)

กฎเหล็ก ต้องปฏิบัติตามอย่างเคร่งครัด:
1. ใช้เฉพาะข้อมูลที่ปรากฏจริงในไฟล์ PDF ที่แนบมาเท่านั้น ห้ามสมมติ คาดเดา หรือเติมตัวเลข/เงื่อนไขที่ไม่มีในเอกสารเด็ดขาด
2. ถ้า Vendor รายใดไม่มีไฟล์แนบ หรืออ่านค่าราคาไม่ได้ ให้ตั้ง dataFound เป็น false และห้ามนำ Vendor นั้นมาเป็นตัวเลือกแนะนำ
3. เปรียบเทียบโดยพิจารณาอย่างน้อย: ราคาสุทธิรวม VAT (grand total), เงื่อนไขการชำระเงิน, ระยะเวลาส่งมอบ, ความตรงของสเปก/รุ่นสินค้ากับที่ระบุในเอกสาร (ถ้ามี)
4. ห้ามเลือก Vendor ที่ราคาต่ำสุดโดยอัตโนมัติ — ถ้าราคาต่ำสุดแต่เงื่อนไขอื่นด้อยกว่าอย่างมีนัยสำคัญ (เช่น เดลิเวอรีนานผิดปกติ, เงื่อนไขชำระเงินไม่เหมาะสม) ให้ชี้แจงเหตุผลตรงไปตรงมาในการแนะนำ
5. recommendationReason ทุกประโยคต้องอ้างอิงได้กับข้อมูลจริงในเอกสาร ห้ามใช้ความเห็นที่ไม่มีหลักฐานรองรับ
6. หากข้อมูลไม่พอสำหรับสรุปว่า Vendor ใดดีที่สุด (เช่น มีข้อมูลไม่ครบ 2 ราย) ให้ recommendedVendorIndex เป็น null และอธิบายเหตุผลใน warnings แทน

ตอบกลับเป็น JSON เท่านั้น ตาม schema ที่กำหนด ห้ามมีข้อความอื่นนอกเหนือ JSON
""";

        parts.Add(new { text = prompt });

        var requestBody = new
        {
            contents = new[] { new { parts = parts.ToArray() } },
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        vendors = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    vendorIndex = new { type = "INTEGER" },
                                    dataFound = new { type = "BOOLEAN" },
                                    vendorName = new { type = "STRING" },
                                    price = new { type = "NUMBER" },
                                    paymentTerm = new { type = "STRING" },
                                    deliveryTerm = new { type = "STRING" },
                                    notes = new { type = "STRING" }
                                }
                            }
                        },
                        recommendedVendorIndex = new { type = "INTEGER" },
                        recommendationReason = new { type = "STRING" },
                        warnings = new { type = "ARRAY", items = new { type = "STRING" } }
                    }
                }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var http = _httpFactory.CreateClient("Gemini");
            var res = await http.PostAsync(url, content);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync();
                var errorId1 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId1}] GenerateVendorHintAsync: Gemini API error {res.StatusCode} — {errBody}")); } catch { }
                return new() { Success = false, ErrorMessage = $"Gemini API error: {res.StatusCode} — {errBody} (Error ID: {errorId1}, กรุณาแจ้ง Programmer)" };
            }

            var resBody = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resBody);
            var text = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

            var parsed = JsonSerializer.Deserialize<JsonElement>(text!);

            var vendors = new List<VendorHintDetailDto>();
            if (parsed.TryGetProperty("vendors", out var vArr))
            {
                foreach (var v in vArr.EnumerateArray())
                {
                    vendors.Add(new VendorHintDetailDto
                    {
                        VendorIndex = v.TryGetProperty("vendorIndex", out var vi) ? vi.GetInt32() : 0,
                        DataFound = v.TryGetProperty("dataFound", out var df) && df.GetBoolean(),
                        VendorName = v.TryGetProperty("vendorName", out var vn) ? vn.GetString() : null,
                        Price = v.TryGetProperty("price", out var pr) ? ParseDecimalFromJson(pr) : null,
                        PaymentTerm = v.TryGetProperty("paymentTerm", out var pt) ? pt.GetString() : null,
                        DeliveryTerm = v.TryGetProperty("deliveryTerm", out var dt) ? dt.GetString() : null,
                        Notes = v.TryGetProperty("notes", out var nt) ? nt.GetString() : null,
                    });
                }
            }

            return new VendorQuoteAiHintResultDto
            {
                Success = true,
                Vendors = vendors,
                RecommendedVendorIndex = parsed.TryGetProperty("recommendedVendorIndex", out var ri) && ri.ValueKind == JsonValueKind.Number ? ri.GetInt32() : null,
                RecommendationReason = parsed.TryGetProperty("recommendationReason", out var rr) ? rr.GetString() : null,
                Warnings = parsed.TryGetProperty("warnings", out var wArr)
                    ? wArr.EnumerateArray().Select(w => w.GetString() ?? "").ToList()
                    : null,
            };
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            var errorId2 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId2}] GenerateVendorHintAsync timeout: {ex.Message}")); } catch { }
            return new() { Success = false, ErrorMessage = $"Gemini ใช้เวลาวิเคราะห์นานเกินไป (timeout) กรุณาลองใหม่ หรือลดจำนวนไฟล์ที่วิเคราะห์พร้อมกัน (Error ID: {errorId2}, ถ้ายังไม่หาย กรุณาแจ้ง Programmer)" };
        }
        catch (Exception ex)
        {
            var errorId3 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            try { await _audit.LogAsync("-", ErrorStatus, "system", "", "", TruncateForRemark($"[ERROR:{errorId3}] GenerateVendorHintAsync: {ex.GetType().Name} - {ex.Message}")); } catch { }
            return new() { Success = false, ErrorMessage = $"วิเคราะห์ไม่สำเร็จ: {ex.Message} (Error ID: {errorId3}, กรุณาแจ้ง Programmer)" };
        }
    }

    public async Task SaveUserChoiceAsync(int docRequestId, int itemId, Guid userId, int chosenVendorQuoteId, string? reason)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items).ThenInclude(i => i.VendorQuotes)
            .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.RequesterId != userId) throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์");
            if (doc.Status != "user_confirm") throw new InvalidOperationException($"เลือกไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            var item = doc.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new KeyNotFoundException("ไม่พบ Item");
            var chosen = item.VendorQuotes.FirstOrDefault(v => v.Id == chosenVendorQuoteId) ?? throw new KeyNotFoundException("ไม่พบ Vendor Quote");
            var recommended = item.VendorQuotes.FirstOrDefault(v => v.IsRecommended);
            var differs = recommended != null && recommended.Id != chosen.Id;

            if (differs && string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("กรุณากรอกเหตุผลเมื่อเลือก Vendor ที่ไม่ตรงกับคำแนะนำของฝ่ายจัดซื้อ");

            foreach (var vq in item.VendorQuotes)
            {
                vq.IsUserSelected = vq.Id == chosen.Id;
                vq.UserDiffReason = vq.Id == chosen.Id && differs ? reason!.Trim() : null;
                vq.UpdatedAt = DateTime.UtcNow;
            }
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            // Audit: save user choice
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "user", await GetUserEmailAsync(userId), await GetUserEmailAsync(doc.PurchaserId), $"SaveUserChoice Item:{itemId} Chosen:{chosenVendorQuoteId}"); } catch { }

        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SaveUserChoiceAsync), doc?.RfqNo, doc?.Status, "user", userId, doc?.PurchaserId, $"docRequestId={docRequestId}, itemId={itemId}, chosenVendorQuoteId={chosenVendorQuoteId}");
            throw;
        }
    }

    public async Task<string> UserConfirmAsync(int docRequestId, Guid userId, List<UserConfirmItemDto> items)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items).ThenInclude(i => i.VendorQuotes)
            .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.RequesterId != userId) throw new UnauthorizedAccessException("คุณไม่มีสิทธิ์");
            if (doc.Status != "user_confirm") throw new InvalidOperationException($"ยืนยันไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            foreach (var payloadItem in items)
            {
                var item = doc.Items.FirstOrDefault(i => i.Id == payloadItem.ItemId);
                if (item is null || payloadItem.ChosenVendorQuoteId is null) continue;

                var recommended = item.VendorQuotes.FirstOrDefault(v => v.IsRecommended);
                var chosen = item.VendorQuotes.FirstOrDefault(v => v.Id == payloadItem.ChosenVendorQuoteId);
                if (chosen is null) continue;
                var differs = recommended != null && recommended.Id != chosen.Id;

                if (differs && string.IsNullOrWhiteSpace(payloadItem.DifferReason))
                    throw new InvalidOperationException($"Item {item.Id}: กรุณากรอกเหตุผลเมื่อเลือก Vendor ไม่ตรงกับคำแนะนำ");

                foreach (var vq in item.VendorQuotes)
                {
                    vq.IsUserSelected = vq.Id == chosen.Id;
                    vq.UserDiffReason = vq.Id == chosen.Id && differs ? payloadItem.DifferReason!.Trim() : null;
                }
            }

            var missing = doc.Items.Where(i => !i.VendorQuotes.Any(v => v.IsUserSelected)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"กรุณาเลือก Vendor ให้ครบทุก Item ก่อนยืนยัน (ขาด {missing.Count} Item)");

            doc.Status = "cost_saving";
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: submit draft -> user_pending
            try { await _audit.LogAsync(doc.RfqNo, "user_confirm", "user", await GetUserEmailAsync(userId), await GetUserEmailAsync(doc.PurchaserId), "Submit User Confirmation"); } catch { }

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(UserConfirmAsync), doc?.RfqNo, doc?.Status, "user", userId, doc?.PurchaserId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    // ── Cost Saving ─────────────────────────────────────────────────
    public async Task SaveCostSavingAsync(int docRequestId, int itemId, Guid purchaserId, CostSavingItemDto dto)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "cost_saving")
                throw new InvalidOperationException($"บันทึกไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            var item = doc.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new KeyNotFoundException("ไม่พบ Item");

            int selectedId;
            decimal? selectedPrice = null;


            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ""Id"", ""Price"", ""FinalQuotationFilePath"" FROM ""VendorQuotes"" WHERE ""DocRequestItemId"" = @p_item AND ""IsUserSelected"" = TRUE LIMIT 1";
                var p = cmd.CreateParameter(); p.ParameterName = "@p_item"; p.Value = itemId; cmd.Parameters.Add(p);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("Item นี้ยังไม่มี Vendor ที่ User เลือกไว้");

                selectedId = reader.GetInt32(0);

                if (!await reader.IsDBNullAsync(1))
                {
                    var fieldType = reader.GetFieldType(1);
                    if (fieldType == typeof(decimal))
                    {
                        selectedPrice = reader.GetDecimal(1);
                    }
                    else
                    {
                        var s = reader.GetString(1);
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                                selectedPrice = d;
                            else if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out d))
                                selectedPrice = d;
                        }
                    }
                }
            }

            var differs = dto.FinalPrice.HasValue && dto.FinalPrice != selectedPrice;
            if (differs && string.IsNullOrWhiteSpace(dto.CostSavingReason))
                throw new InvalidOperationException("กรุณากรอกเหตุผลเมื่อราคาสุดท้ายไม่ตรงกับราคาที่ User เลือกไว้");

            var vq = new SmartRFQ.API.Models.VendorQuote { Id = selectedId };
            db.VendorQuotes.Attach(vq);
            vq.FinalPrice = dto.FinalPrice;
            vq.FinalDiscount = dto.FinalDiscount;
            vq.FinalRemark = dto.FinalRemark;
            vq.CostSavingReason = differs ? dto.CostSavingReason!.Trim() : null;
            vq.UpdatedAt = DateTime.UtcNow;

            if (dto.FinalQuotationFile != null)
            {
                vq.FinalQuotationFilePath = await SaveFileAsync(dto.FinalQuotationFile, "cost_saving");
                db.Entry(vq).Property(x => x.FinalQuotationFilePath).IsModified = true;
            }
            else if (dto.RemoveFinalQuotationFile)
            {
                vq.FinalQuotationFilePath = null;
                db.Entry(vq).Property(x => x.FinalQuotationFilePath).IsModified = true;
            }

            db.Entry(vq).Property(x => x.FinalPrice).IsModified = true;
            db.Entry(vq).Property(x => x.FinalDiscount).IsModified = true;
            db.Entry(vq).Property(x => x.FinalRemark).IsModified = true;
            db.Entry(vq).Property(x => x.CostSavingReason).IsModified = true;

            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            // Audit: save cost saving for item
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), $"SaveCostSaving Item:{itemId} FinalPrice:{dto.FinalPrice}"); } catch { }
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(SaveCostSavingAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}, itemId={itemId}");
            throw;
        }
    }

    // Purchase Confirm

    public async Task<string> ConfirmCostSavingAsync(int docRequestId, Guid purchaserId)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "cost_saving")
                throw new InvalidOperationException($"ยืนยันไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            // For each item, ensure there exists a VendorQuote with IsUserSelected and a non-null FinalPrice
            var itemIds = doc.Items.Select(i => i.Id).ToList();
            var missing = await db.DocRequestItems
                .Where(i => i.DocRequestId == docRequestId)
                .Where(i => !db.VendorQuotes.Any(v => v.DocRequestItemId == i.Id && v.IsUserSelected && EF.Property<decimal?>(v, "FinalPrice") != null))
                .ToListAsync();

            if (missing.Count > 0)
                throw new InvalidOperationException($"กรุณากรอก Final Price ให้ครบทุก Item (ขาด {missing.Count} Item)");

            doc.Status = "purchase_confirm";
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: cost saving confirmed — moved to purchase_confirm
            try { await _audit.LogAsync(doc.RfqNo, "cost_saving", "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), "Confirm Cost Saving"); } catch { }




            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(ConfirmCostSavingAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    public async Task<string> BackToCompareAsync(int docRequestId, Guid purchaserId)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "cost_saving" && doc.Status != "purchase_confirm")
                throw new InvalidOperationException($"ย้อนกลับไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            doc.Status = "purchase_compare";
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: moved back to purchase_compare
            try { await _audit.LogAsync(doc.RfqNo, doc.Status, "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), "Back to compare"); } catch { }

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(BackToCompareAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}");
            throw;
        }
    }

    public async Task<string> ApprovePurchaseConfirmAsync(int docRequestId, int itemId, Guid purchaserId)
    {
        DocRequest? doc = null;
        try
        {
            doc = await db.DocRequests.FirstOrDefaultAsync(d => d.Id == docRequestId) ?? throw new KeyNotFoundException("ไม่พบ RFQ");

            if (doc.Status != "purchase_confirm")
                throw new InvalidOperationException($"อนุมัติไม่ได้ สถานะปัจจุบัน: {doc.Status}");

            // Mark as finished
            doc.Status = "finish";
            doc.FinishedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Audit: purchase confirm approved -> finished
            try { await _audit.LogAsync(doc.RfqNo, "purchase_confirm", "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), $"Approve purchase confirm Item:{itemId}"); } catch { }
            try { await _audit.LogAsync(doc.RfqNo, "finish", "purchase", await GetUserEmailAsync(purchaserId), await GetUserEmailAsync(doc.RequesterId), $"Finish purchase confirm Item:{itemId}"); } catch { }

            return doc.RfqNo;
        }
        catch (Exception ex)
        {
            await LogOperationErrorAsync(ex, nameof(ApprovePurchaseConfirmAsync), doc?.RfqNo, doc?.Status, "purchase", purchaserId, doc?.RequesterId, $"docRequestId={docRequestId}, itemId={itemId}");
            throw;
        }
    }
}