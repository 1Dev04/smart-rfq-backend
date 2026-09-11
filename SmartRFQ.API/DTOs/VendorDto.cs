using System.ComponentModel.DataAnnotations;

namespace SmartRFQ.API.DTOs;

public class VendorQuoteDto
{
    public int? Id { get; set; }
    [Required] public string VendorName { get; set; } = "";
    public string? ItemDescription { get; set; }
    public string? SpecPartNo { get; set; }
    public string? Model { get; set; }
    public string? BuyerEmail { get; set; }
    public decimal? Price { get; set; }
    public string? Remark { get; set; }
    public decimal? Discount { get; set; }
    public bool IsRecommended { get; set; } = false;
    public IFormFile? QuotationFile { get; set; }
    public bool RemoveQuotationFile { get; set; } = false;
}

public class VendorQuoteAiExtractResultDto
{
    public string? VendorName { get; set; }
    public string? ItemDescription { get; set; }
    public string? SpecPartNo { get; set; }
    public string? Model { get; set; }
    public string? BuyerEmail { get; set; }
    public decimal? Price { get; set; }
    public string? Remark { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

}

public class VendorHintFileDto
{
    public IFormFile? File { get; set; }
    public string? ExistingFilePath { get; set; }
    public string? VendorName { get; set; }

}

public class VendorQuoteAiHintResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<VendorHintDetailDto>? Vendors { get; set; }
    public int? RecommendedVendorIndex { get; set; } // 1,2,3 ตรงกับคอลัมน์ Vendor
    public string? RecommendationReason { get; set; }
    public List<string>? Warnings { get; set; }

}

public class VendorHintDetailDto
{
    public int VendorIndex { get; set; }
    public bool DataFound { get; set; }
    public string? VendorName { get; set; }
    public decimal? Price { get; set; }
    public string? PaymentTerm { get; set; }
    public string? DeliveryTerm { get; set; }
    public string? Notes { get; set; }
}

public class CostSavingItemDto
{

    public decimal? FinalPrice { get; set; }
    public decimal? FinalDiscount { get; set; }
    public string? FinalRemark { get; set; }
    public string? CostSavingReason { get; set; }
    public IFormFile? FinalQuotationFile { get; set; }
    public bool RemoveFinalQuotationFile { get; set; } = false;
}