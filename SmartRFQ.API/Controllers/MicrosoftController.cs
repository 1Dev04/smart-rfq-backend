
using Microsoft.AspNetCore.Mvc;

using System.Text.Json;

namespace SmartRFQ.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MicrosoftController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public static readonly HashSet<string> AllowedEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "krittiphon.y@live.ku.th"
    };

    public MicrosoftController(IConfiguration config, IHttpClientFactory factory)
    {
        _config = config;
        _http = factory.CreateClient();
    }

    [HttpPost("/microsoft/callback")]
    public async Task<IActionResult> MicrosoftCallback([FromBody] CallbackRequest req)
    {
        try
        {
            // var tenantId = _config["AzureAd:TenantId"] ?? throw new Exception("ClientId not configured");
            var clientId = _config["AzureAd:ClientId"] ?? throw new Exception("ClientSecret not configured");
            var clientSecret = _config["AzureAd:ClientSecret"] ?? throw new Exception("TenantId not configured");
            var redirectUri = Environment.GetEnvironmentVariable("AzureAd__CallbackPath") 
    ?? "http://localhost:5173/auth/microsoft/callback";


            var tenantId = _config["AzureAd:TenantId"] ?? throw new Exception("TenantId not configured");

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

            var jwtToken = $"ms-token-{Guid.NewGuid()}";



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