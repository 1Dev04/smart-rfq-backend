using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartRFQ.API.Data;
using SmartRFQ.API.DTOs;
using SmartRFQ.API.Models;

namespace SmartRFQ.API.Services;

public interface IAuthService
{
    Task<(AuthResponseDto? user, string? error)> LoginAsync(LoginDto dto, HttpResponse response);
    Task<bool> RefreshAsync(HttpRequest request, HttpResponse response);
    Task RevokeAsync(HttpRequest request, HttpResponse response);
}



public class AuthService(AppDbContext db, IConfiguration cfg) : IAuthService
{
    private CookieOptions AccessCookieOpts => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddHours(8)
    };
    private CookieOptions RefreshCookieOpts(bool rememberMe) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = rememberMe
        ? DateTime.UtcNow.AddDays(30)
        : null,
        Path = "/api/auth",
    };

    // Login 
    public async Task<(AuthResponseDto?, string?)> LoginAsync(LoginDto dto, HttpResponse res)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return (null, "Invalid email or password");

        SetTokenCookies(user, res, dto.RememberMe);

        return (new AuthResponseDto(user.FullName, user.Email, user.Role), null);
    }

    public async Task<bool> RefreshAsync(HttpRequest req, HttpResponse res)
    {
        var token = req.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(token)) return false;

        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token
                                   && !r.IsRevoked
                                   && r.Expires > DateTime.UtcNow);
        if (stored is null) return false;

        // revoke
        stored.IsRevoked = true;
        bool rememberMe = stored.Expires > DateTime.UtcNow.AddDays(1);
        SetTokenCookies(stored.User, res, rememberMe);
        await db.SaveChangesAsync();
        return true;
    }

    // Revoke (logout)
    public async Task RevokeAsync(HttpRequest req, HttpResponse res)
    {
        var token = req.Cookies["refresh_token"];
        if (!string.IsNullOrEmpty(token))
        {
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
            if (stored is not null) stored.IsRevoked = true;
            await db.SaveChangesAsync();
        }
        res.Cookies.Delete("access_token");
        res.Cookies.Delete("refresh_token");
    }

    // Helper
    private void SetTokenCookies(User user, HttpResponse res, bool rememberMe)
    {
        var jwt = GenerateJwt(user);
        var refreshExpiry = rememberMe
            ? DateTime.UtcNow.AddDays(30)
            : DateTime.UtcNow.AddHours(8); 

        var refresh = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            Expires = refreshExpiry,
            UserId = user.Id,
        };

        db.RefreshTokens.Add(refresh);
        db.SaveChanges();

        res.Cookies.Append("access_token", jwt, AccessCookieOpts);
        res.Cookies.Append("refresh_token", refresh.Token, RefreshCookieOpts(rememberMe));
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
          new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
          new Claim(ClaimTypes.Email, user.Email),
          new Claim(ClaimTypes.Name, user.FullName),
          new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
};
