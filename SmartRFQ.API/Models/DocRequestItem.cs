namespace SmartRFQ.API.Models;

public class DocRequestItem
{
    public int Id { get; set; }

    // FK → DocRequest
    public int DocRequestId { get; set; }
    public DocRequest DocRequest { get; set; } = null!;

    // ── General Info ──
    public DateTime? TargetPURreply { get; set; }
    public string ProjectName { get; set; } = "";
    public string GlCode { get; set; } = "";
    public string? SapItem { get; set; }
    public string ItemDescription { get; set; } = "";
    public string SpecPartNo { get; set; } = "";
    public string? Model { get; set; }
    public string Brand { get; set; } = "";
    public string ForGas { get; set; } = "";

    // ── Product Specification ──
    public string Type { get; set; } = "";
    public string SpecPurity { get; set; } = "";
    public string CylinderType { get; set; } = "";
    public int Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string CylinderSize { get; set; } = "";
    public string MakerSource { get; set; } = "";
    public string RequiredValve { get; set; } = "";
    public string PurposeApplication { get; set; } = "";
    public string Customer { get; set; } = "";
    public string AddressLocation { get; set; } = "";
    public string RecommendVendor { get; set; } = "";
    public string Remark { get; set; } = "";

    // ── File Paths (เก็บ path หลัง upload) ──
    public string? AttachDwgPath { get; set; }
    public string? AttachSpecPath { get; set; }
    public string? AttachQuotationPath { get; set; }
    public string? AttachEtcPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}