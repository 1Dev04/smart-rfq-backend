namespace SmartRFQ.API.Models;

public class AuditLogQuery{
    public DateTime? DateFrom {get; set;}
    public DateTime? DateTo {get; set;}
    public string? E_User {get; set;}
    public string? E_Purchaser {get; set;}
    public string? RfqNo {get; set;}
    public int Qty {get; set;}
    public string? Status {get; set;}
    public int Page {get; set;} = 1;
    public int PageSize {get; set;} = 10;
} 