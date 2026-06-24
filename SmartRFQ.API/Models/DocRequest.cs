using SmartRFQ.API.Models;

public class DocRequest
{
    public int Id { get; set; }
    public string RfqNo { get; set; } = "";
    public string RevNo { get; set; } = "Rev.00";
    public string Status { get; set; } = "user_fill";
    public int? LeadTime { get; set; }


    // FK
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    public Guid? PurchaserId { get; set; }
    public User? Purchaser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

      public ICollection<DocRequestItem> Items { get; set; } = new List<DocRequestItem>();
}

public class GLCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}