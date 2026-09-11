
using Microsoft.AspNetCore.Mvc;
using SmartRFQ.API.Services;
using SmartRFQ.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SmartRFQ.API.Controllers;

[ApiController]
[Route("api/[controller]")]

public class MicrosoftController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly JwtService _jwtService;
    private readonly AppDbContext _db;

    public static readonly HashSet<string> AllowedEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "krittiphon.y@live.ku.th"
    };

    public MicrosoftController(IConfiguration config, IHttpClientFactory factory, JwtService jwtService, AppDbContext db)
    {
        _config = config;
        _http = factory.CreateClient();
        _jwtService = jwtService;
        _db = db;
    }

    [HttpPost("/auth/microsoft/callback")]
    public async Task<IActionResult> MicrosoftCallback([FromBody] CallbackRequest req)
    {
        try
        {
            var tenantId = _config["AzureAd:TenantId"] ?? throw new Exception("ClientId not configured");
            var clientId = _config["AzureAd:ClientId"] ?? throw new Exception("ClientSecret not configured");
            var clientSecret = _config["AzureAd:ClientSecret"] ?? throw new Exception("TenantId not configured");
            var redirectUri = Environment.GetEnvironmentVariable("AzureAd__CallbackPath")
    ?? "http://localhost:5173/auth/microsoft/callback";




            var tokenRes = await _http.PostAsync(
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId!,
                    ["client_secret"] = clientSecret!,
                    ["code"] = req.Code,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = req.CodeVerifier,
                })
            );
            if (!tokenRes.IsSuccessStatusCode)
            {
                var errBody = await tokenRes.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Token error: {errBody}");
                return Unauthorized(new { message = "Invalid Token Failed", detail = errBody });
            }

            var tokenJson = await tokenRes.Content.ReadAsStringAsync();
            var tokenDoc = JsonDocument.Parse(tokenJson);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

            // Fetch data user จาก Microsoft Graph
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var graphReq = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            graphReq.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var graphRes = await _http.SendAsync(graphReq);
            var graphJson = await graphRes.Content.ReadAsStringAsync();
            var graphDoc = JsonDocument.Parse(graphJson);

            var email = (graphDoc.RootElement.TryGetProperty("mail", out var mailEl)
                    ? mailEl.GetString()
                    : graphDoc.RootElement.GetProperty("userPrincipalName").GetString()) ?? "";

            var displayName = graphDoc.RootElement.GetProperty("displayName").GetString() ?? "";

            // Check Whitelist
            if (string.IsNullOrEmpty(email) || !AllowedEmails.Contains(email)) return Forbid();

            var role = "user";

            // Find or create a local User record so we can set NameIdentifier (GUID)
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new SmartRFQ.API.Models.User
                {
                    Email = email,
                    FullName = displayName,
                    Role = role,
                    IsActive = true
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            var jwtToken = _jwtService.GenerateToken(email, displayName, role, user.Id.ToString());

            // Set access_token cookie so frontend flows that rely on cookie-based
            // authentication (normal login) will work after Microsoft login.
            Response.Cookies.Append("access_token", jwtToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
                Expires = DateTime.UtcNow.AddHours(8)
            });

            return Ok(new
            {
                token = jwtToken,
                role = role,
                email = email,
                displayName = displayName,
            });



        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}

public class CallbackRequest
{
    public string Code { get; set; } = "";
    public string CodeVerifier { get; set; } = "";
}