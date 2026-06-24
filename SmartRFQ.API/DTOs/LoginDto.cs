using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SmartRFQ.API.DTOs;

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(4), MaxLength(6)] string Password,
    bool RememberMe = false
);

public class StrongPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
       var pwd = value as string ?? "";
       if (pwd.Length < 4 || pwd.Length > 6) return new ValidationResult("Password must be 4-6 characters.");
       if (!Regex.IsMatch(pwd, @"[a-z]"))return new ValidationResult("Password must contain a lowercase letter.");
       if (!Regex.IsMatch(pwd, @"A-Z"))return new ValidationResult("Password must contain an uppercase letter.");
       if (!Regex.IsMatch(pwd, @"\d")) return new ValidationResult("Password must contain a digit.");
       return ValidationResult.Success;
    }
}

