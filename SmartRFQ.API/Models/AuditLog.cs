using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SmartRFQ.API.Models;

[Index(nameof(DateTime))]
[Index(nameof(E_User))]
[Index(nameof(RfqNo))]
[Index(nameof(Status))]
public class AuditLogs  
{
    [Key]
    public int Id { get; set; }

    // RFQ reference
    public int?   DocRequestId { get; set; }

    [MaxLength(30)]
    public string RfqNo  { get; set; } = "";

    // Action / Status
    [MaxLength(20)]
    public string Status { get; set; } = "";   // Waiting|Accept|Resent|Cancel|Create

    // Who did it
    [MaxLength(20)]
    public string Role   { get; set; } = "";   // User | Purchase | Admin

    [MaxLength(100)]
    public string E_User { get; set; } = "";   // email ของคนทำ

    [MaxLength(100)]
    public string E_Purchaser { get; set; } = ""; // email purchaser

    [MaxLength(255)]
    public string Remark { get; set; } = "";   // note/remark

    public DateTime DateTime { get; set; } = DateTime.UtcNow;
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