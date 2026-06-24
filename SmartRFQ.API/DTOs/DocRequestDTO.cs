using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace SmartRFQ.API.DTOs;

// POST /api/doc-request/create
public class CreateDocRequestItemDto
{
    [Required] public string TargetPURreply {get; set; } = "";
    [Required, MaxLength(150)] public string ProjectName {get; set; } = "";
    [Required] public string GlCode {get; set; } = "";
    public string? SapItem {get; set;}
    [Required, MaxLength(500)] public string ItemDescription {get; set;} = ""; 
    [Required, MaxLength(200)] public string SpecPartNo {get; set;} = "";
    [MaxLength(100)] public string? Model {get; set;}
    [Required, MaxLength(100)] public string Brand {get; set;} = "" ;
    [Required, MaxLength(100)] public string ForGas {get; set;} = "";
    [Required] public string Type {get; set;} = "";
    [Required, MaxLength(100)] public string SpecPurity {get; set;} = "";
    [Required, MaxLength(100)] public string CylinderType {get; set;} = "";
    [Required, Range(1, 999999)] public int Quantity {get; set;}
    [Required, MaxLength(20)] public string Uom {get; set;} = "";
    [Required, MaxLength(20)] public string CylinderSize {get; set;} = "";
    [Required, MaxLength(150)] public string MakerSource {get; set;} = "";
    [Required, MaxLength(100)] public string RequiredValve {get; set;} = "";
    [Required, MaxLength(300)] public string PurposeApplication {get; set;} = "";
    [Required, MaxLength(150)] public string Customer {get; set;} = "";
    [Required, MaxLength(300)] public string AddressLocation {get; set;} = "";
    [Required, MaxLength(200)] public string RecommendVendor {get; set;} = "";
    [Required, MaxLength(500)] public string Remark {get; set;} = "";

    // Files
    public IFormFile? AttachDWG {get; set;}
    public IFormFile? AttachSpec {get; set;}
    public IFormFile? AttachQuotation {get; set;}
    public IFormFile? AttachEtc {get; set;}

}


public record DocRequestListRowDto(
    string CreatedAt,           // วันที่ขอ
    string RfqNo,               // RFQ No. (link)
    int DocRequestId,           // สำหรับ click เปิด drawer
    string ItemType,            // Item Type
    string ItemDescription,     // รายการ
    int Quantity,               // จำนวน
    string Uom,                 // หน่วย
    string Status,              // สถานะ
    string RequesterEmail,      // ผู้ขอ RFQ
    string? PurchaserEmail,     // ผู้รับผิดชอบ
    int? LeadTimeDays,          // ระยะเวลา (วัน)
    bool IsFirstItemOfRfq       // สำหรับ merge cell วันที่+RFQNo ใน frontend
);


// Get /api/doc-request
public record DocRequestListResponseDto(
    List<DocRequestListRowDto> Rows,
    int Total,
    int Page,
    int PageSize
);

public record DocRequestItemResponseDto(
    int Id, 
    string ProjectName,
    string GlCode,
    string? SapItem,
    string ItemDescription,
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
    int Page = 1,
    int PageSize = 20
);