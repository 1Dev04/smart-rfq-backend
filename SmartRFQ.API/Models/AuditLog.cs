using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;

namespace SmartRFQ.API.Models;

[Index(nameof(DateTime))]
[Index(nameof(E_User))]
[Index(nameof(RfqNo))]
[Index(nameof(Status))]

public class AudioLogs {
    [Key]
    public int Id {get; set;}
    [MaxLength(20)]
    public string RfqNo {get; set;} = "";
    [MaxLength(20)]
    public string Status {get; set;} = "";
    [MaxLength(20)]
    public string Role {get; set;} = "";
    [MaxLength(100)]
    public string E_User {get; set;} = "";
    [MaxLength(100)]
    public string E_Purchaser {get; set;} = "";
    [MaxLength(255)]
    public string Remark {get; set;} = "";
    public DateTime DateTime {get; set;} = DateTime.UtcNow;
}