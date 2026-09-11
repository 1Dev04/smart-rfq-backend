namespace SmartRFQ.API.Models;

public class VendorQuote
{
    public int Id {get; set;}
    public int DocRequestItemId {get; set;}
    public DocRequestItem DocRequestItem {get; set;} = null!;
    
    public string? VendorName {get; set;}
    public string? ItemDescription { get; set; }
    public string? SpecPartNo { get; set; }
    public string? Model {get; set;}
    public string? BuyerEmail {get; set;}
    public string? Remark {get; set;}

    public decimal? Price {get; set;}
    public decimal? Discount {get; set;}
    public string? QuotationFilePath {get; set;}
    public bool IsRecommended {get; set;} = false;

    public string? FinalRemark {get; set;}
    public decimal? FinalPrice {get; set;}
    public decimal? FinalDiscount {get; set;}
    public string? FinalQuotationFilePath {get; set;}
    public string? CostSavingReason {get; set;}

    
    public bool IsUserSelected { get; set;}
    public string? UserDiffReason {get; set;}

    public DateTime CreatedAt {get; set;} = DateTime.UtcNow;
    public DateTime UpdatedAt {get; set;}


}