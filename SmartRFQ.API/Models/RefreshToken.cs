namespace SmartRFQ.API.Models;

using System;

public class RefreshToken
{
    public Guid Id  {get; set;} = Guid.NewGuid();
    public string Token {get; set;} = "";
    public DateTime Expires {get; set;}
    public bool IsRevoked {get; set;} = false;
    public DateTime CreatedAt {get; set;} = DateTime.UtcNow;

    public Guid UserId {get; set;}
    public User User {get; set;} = null!;
}