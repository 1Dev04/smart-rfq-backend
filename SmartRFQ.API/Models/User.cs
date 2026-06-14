
using System;
using System.Collections.Generic;
namespace SmartRFQ.API.Models;

public class User
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Email        { get; set; } = "";
    public string   PasswordHash { get; set; } = "";
    public string   Role         { get; set; } = "User"; 
    public string   FullName     { get; set; } = "";
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

}