
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SmartRFQ.API.DTOs;
namespace SmartRFQ.API.DTOs;

// POST /api/doc-request/create
public class CreateDocRequestItemDto
{
    [Required] public string TargetPURreply { get; set; } = "";

    // ── ฟิลด์ที่เปิด/ปิดตาม Type จาก Frontend — ไม่บังคับฝั่ง Backend แล้ว ──
    [MaxLength(150)] public string? ProjectName { get; set; }
    public string? GlCode { get; set; }
    public string? SapItem { get; set; }
    [MaxLength(500)] public string? ItemDescription { get; set; }
    [MaxLength(200)] public string? SpecPartNo { get; set; }
    [MaxLength(100)] public string? Model { get; set; }
    [MaxLength(100)] public string? Brand { get; set; }
    [MaxLength(100)] public string? ForGas { get; set; }

    [Required] public string Type { get; set; } = "";

    [MaxLength(100)] public string? SpecPurity { get; set; }
    [MaxLength(100)] public string? CylinderType { get; set; }

    [Required, Range(1, 999999)] public int Quantity { get; set; }
    [Required, MaxLength(20)] public string Uom { get; set; } = "";

    [MaxLength(20)] public string? CylinderSize { get; set; }
    [MaxLength(150)] public string? MakerSource { get; set; }
    [MaxLength(100)] public string? RequiredValve { get; set; }
    [MaxLength(300)] public string? PurposeApplication { get; set; }
    [MaxLength(150)] public string? Customer { get; set; }
    [MaxLength(300)] public string? AddressLocation { get; set; }

    // Recommend Vendor ยังกรอกได้เสมอทุก Type แต่ไม่บังคับกรอกจริงจัง ก็เผื่อไว้เป็น optional เหมือนกัน
    [MaxLength(200)] public string? RecommendVendor { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }

    public IFormFile? AttachDWG { get; set; }
    public IFormFile? AttachSpec { get; set; }
    public IFormFile? AttachQuotation { get; set; }
    public IFormFile? AttachEtc { get; set; }
}

public record ItemVendorEmailDto(int ItemId, List<string> Emails);
public record SendQuotationDto(List<ItemVendorEmailDto> Items);

public record DocRequestListRowDto(
    string CreatedAt,           // วันที่ขอ
    string? UpdatedAt,
    string RfqNo,               // RFQ No. (link)
    int DocRequestId,           // สำหรับ click เปิด drawer
    string ItemType,            // Item Type
    string? ItemDescription,    // รายการ
    int Quantity,                // จำนวน
    string Uom,                  // หน่วย
    string Status,               // สถานะ
    string RequesterEmail,      // ผู้ขอ RFQ
    string? PurchaserEmail,     // ผู้รับผิดชอบ
    int? LeadTimeDays,          // ระยะเวลา (วัน)
    bool IsFirstItemOfRfq,      // สำหรับ merge cell วันที่+RFQNo ใน frontend
    int TotalItems,
    bool WasRejected 
);

// Get /api/doc-request
public record DocRequestListResponseDto(
    List<DocRequestListRowDto> Rows,
    int Total,
    int Page,
    int PageSize
);

// DTOs/SaveDraft
public class SaveDraftItemDto
{
    // ── ทุก field เป็น optional หมด เพราะ draft กรอกไม่ครบได้ ──
    public int? Id {get; set;}

    [Required] public string TargetPURreply {get; set;} = "";
    [MaxLength(150)] public string? ProjectName {get; set;}
    public string? GlCode {get; set;}
    public string? SapItem {get; set;}
    [MaxLength(500)] public string? ItemDescription {get; set;}
    [MaxLength(200)] public string? SpecPartNo {get; set;}
    [MaxLength(100)] public string? Model {get; set;}
    [MaxLength(100)] public string? Brand {get; set;}
    [MaxLength(100)] public string? ForGas {get; set;}
    [Required] public string Type {get; set;} = "";
    [MaxLength(100)] public string? SpecPurity {get; set;}
    [MaxLength(100)] public string? CylinderType {get; set;}
    [Required, Range(1, 999999)] public int Quantity {get; set;}
    [Required, MaxLength(20)] public string Uom {get; set;} = "";
    [MaxLength(20)] public string? CylinderSize {get; set;}
    [MaxLength(150)] public string? Customer { get; set; }
    [MaxLength(300)] public string? AddressLocation { get; set; }
    [MaxLength(200)] public string? RecommendVendor { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }

    [MaxLength(150)] public string? MakerSource { get; set; }

    [MaxLength(100)] public string? RequiredValve { get; set; }

    [MaxLength(300)] public string? PurposeApplication { get; set; }
    

    public IFormFile? AttachDWG { get; set; }
    public IFormFile? AttachSpec { get; set; }
    public IFormFile? AttachQuotation { get; set; }
    public IFormFile? AttachEtc { get; set; }


    public bool RemoveAttachDWG { get; set; } = false;
    public bool RemoveAttachSpec { get; set; } = false;
    public bool RemoveAttachQuotation { get; set; } = false;
    public bool RemoveAttachEtc { get; set; } = false;
     
}

public record UpsertDraftResultDto(int Id, string RfqNo);

public record DocRequestItemResponseDto(
    int Id,
    string? ProjectName,
    string? GlCode,
    string? SapItem,
    string? ItemDescription,
    string Type,
    int Quantity,
    string Uom,
    string Status,
    string? TargetPURreply,
    string? AttachDwgPath,
    string? AttachSpecPath,
    string? AttachQuotationPath,
    string? AttachEtcPath
);

// Get
public record DocRequestQueryDto(
    string? Search,
    string? Status,
    string? ItemType,
    string? ContactUser,
    int Page = 1,
    int PageSize = 10
);

public record AcceptDocRequestDto(
    Dictionary<int, int> ItemLeadTimes
);

public record RejectDocRequestDto(string? Reason);
public record CancelDocRequestDto(string? Reason);



public class UpdateDocRequestItemDto
{
    public int? Id {get; set;}
    [Required] public string TargetPURreply {get; set;} = "";
    [MaxLength(150)] public string? ProjectName {get; set;}
    public string? GlCode {get; set;}
    public string? SapItem {get; set;}
    [MaxLength(500)] public string? ItemDescription {get; set;}
    [MaxLength(200)] public string? SpecPartNo {get; set;}
    [MaxLength(100)] public string? Model {get; set;}
    [MaxLength(100)] public string? Brand {get; set;}
    [MaxLength(100)] public string? ForGas {get; set;}
    [Required] public string Type {get; set;} = "";
    [MaxLength(100)] public string? SpecPurity {get; set;}
    [MaxLength(100)] public string? CylinderType {get; set;}
    [Required, Range(1, 999999)] public int Quantity {get; set;}
    [Required, MaxLength(20)] public string Uom {get; set;} = "";
    [MaxLength(20)] public string? CylinderSize {get; set;}
    [MaxLength(150)] public string? Customer { get; set; }
    [MaxLength(300)] public string? AddressLocation { get; set; }
    [MaxLength(200)] public string? RecommendVendor { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }

    [MaxLength(150)] public string? MakerSource { get; set; }

    [MaxLength(100)] public string? RequiredValve { get; set; }

    [MaxLength(300)] public string? PurposeApplication { get; set; }
    

    public IFormFile? AttachDWG { get; set; }
    public IFormFile? AttachSpec { get; set; }
    public IFormFile? AttachQuotation { get; set; }
    public IFormFile? AttachEtc { get; set; }


    public bool RemoveAttachDWG { get; set; } = false;
    public bool RemoveAttachSpec { get; set; } = false;
    public bool RemoveAttachQuotation { get; set; } = false;
    public bool RemoveAttachEtc { get; set; } = false;
     
}

public class UserChooseVendorDto 
{
    public int VendorQuoteId { get; set;}
    public string? Reason {get; set;}
}

public record UserConfirmItemDto(int ItemId, int? ChosenVendorQuoteId, string? DifferReason);
public record UserConfirmPayloadDto(List<UserConfirmItemDto> Items);

