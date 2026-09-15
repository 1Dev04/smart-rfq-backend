using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MimeKit;

namespace SmartRFQ.API.Services;

public record EmailAttachmentRef(string Label, string Url);

public interface IEmailService
{
    Task SendQuotationRequestAsync(
        string toEmail, string? vendorName, string rfqNo, string requester,
        string purchaser, List<EmailAttachmentRef> attachments);
}
public class EmailService(IConfiguration cfg, IHttpClientFactory httpClientFactory) : IEmailService
{
    public async Task SendQuotationRequestAsync(
        string toEmail, string? vendorName, string rfqNo, string requester,
        string purchaser, List<EmailAttachmentRef> attachments)
    {
        var clientId     = cfg["GmailApi:ClientId"]!;
        var clientSecret = cfg["GmailApi:ClientSecret"]!;
        var refreshToken = cfg["GmailApi:RefreshToken"]!;
        var senderEmail  = cfg["GmailApi:SenderEmail"]!;
        var fromName     = cfg["Email:FromName"] ?? "Smart RFQ System";
        var logoUrl      = cfg["Email:LogoUrl"]
            ?? "https://res.cloudinary.com/dxoiu2cn5/image/upload/v1782634393/image0_rjd86g.jpg";

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            Scopes = ["https://www.googleapis.com/auth/gmail.send"],
        });

        var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(flow, senderEmail, token);
        await credential.RefreshTokenAsync(CancellationToken.None);

        var gmail = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Smart RFQ System",
        });

        var greetingName = string.IsNullOrWhiteSpace(vendorName) ? "Vendor" : vendorName;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromName, senderEmail));
        mime.To.Add(MailboxAddress.Parse(toEmail));
        mime.Subject = $"[Smart RFQ] ขอใบเสนอราคา {rfqNo}";

        var builder = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family:'Segoe UI',Helvetica,Arial,sans-serif;max-width:600px;margin:0 auto;background:#f8fafc;padding:24px 16px">
                  <div style="background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #e2e8f0;box-shadow:0 2px 8px rgba(0,0,0,0.04)">
                    <div style="background:linear-gradient(135deg,#1e3a5f,#1d4ed8);padding:28px 32px;text-align:center">
                      <img src="{logoUrl}" alt="Company Logo" style="height:64px;margin-bottom:10px;object-fit:contain" />
                      <div style="color:#ffffff;font-size:18px;font-weight:700;letter-spacing:0.02em">คำขอใบเสนอราคา</div>
                      <div style="color:#bfdbfe;font-size:12px;margin-top:4px;text-transform:uppercase;letter-spacing:0.08em">Request For Quotation</div>
                    </div>
                    <div style="padding:32px">
                      <p style="font-size:14px;color:#1e293b;margin:0 0 6px">เรียน {greetingName},</p>
                      <p style="font-size:14px;color:#475569;line-height:1.6;margin:0 0 24px">
                        บริษัทมีความประสงค์ขอใบเสนอราคาสำหรับคำขอหมายเลข
                        <strong style="color:#1d4ed8">{rfqNo}</strong>
                        กรุณาตรวจสอบรายละเอียดด้านล่างและไฟล์แนบประกอบ
                      </p>
                      <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:24px">
                        <tr>
                          <td style="padding:12px 16px;background:#f1f5f9;border-radius:8px 0 0 8px;font-weight:600;color:#64748b;width:38%">RFQ No.</td>
                          <td style="padding:12px 16px;background:#f8fafc;border-radius:0 8px 8px 0;color:#0f172a;font-weight:600">{rfqNo}</td>
                        </tr>
                        <tr><td colspan="2" style="height:6px"></td></tr>
                        <tr>
                          <td style="padding:12px 16px;background:#f1f5f9;border-radius:8px 0 0 8px;font-weight:600;color:#64748b">ผู้ขอ (Requester)</td>
                          <td style="padding:12px 16px;background:#f8fafc;border-radius:0 8px 8px 0;color:#0f172a">{requester}</td>
                        </tr>
                        <tr><td colspan="2" style="height:6px"></td></tr>
                        <tr>
                          <td style="padding:12px 16px;background:#f1f5f9;border-radius:8px 0 0 8px;font-weight:600;color:#64748b">ผู้ดูแล (Purchase)</td>
                          <td style="padding:12px 16px;background:#f8fafc;border-radius:0 8px 8px 0;color:#0f172a">{purchaser}</td>
                        </tr>
                      </table>
                      <div style="text-align:center;margin:28px 0">
                        <a href="mailto:{purchaser}?subject=Re: [Smart RFQ] ใบเสนอราคา {rfqNo}"
                           style="display:inline-block;background:#1d4ed8;color:#ffffff;text-decoration:none;
                                  font-size:13.5px;font-weight:700;padding:12px 28px;border-radius:8px;
                                  box-shadow:0 4px 12px rgba(29,78,216,0.3)">
                          ✉️ ส่งใบเสนอราคากลับ
                        </a>
                      </div>
                      <p style="font-size:12.5px;color:#94a3b8;text-align:center;margin:0">
                        หรือส่งกลับมาที่ <a href="mailto:{purchaser}" style="color:#1d4ed8">{purchaser}</a>
                      </p>
                    </div>
                    <div style="background:#f8fafc;border-top:1px solid #e2e8f0;padding:16px 32px;text-align:center">
                      <p style="color:#94a3b8;font-size:11px;margin:0">
                        Smart RFQ System — อีเมลนี้ส่งโดยระบบอัตโนมัติ กรุณาอย่าตอบกลับอีเมลนี้โดยตรง
                      </p>
                    </div>
                  </div>
                </div>
                """
        };

        // ── ไฟล์แนบ: แต่ละ label กลายเป็นไฟล์แยกกันในอีเมล ──
        var http = httpClientFactory.CreateClient();

        foreach (var att in attachments.Where(a => !string.IsNullOrWhiteSpace(a.Url)))
        {
            try
            {
                byte[] bytes;

                if (Uri.TryCreate(att.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    bytes = await http.GetByteArrayAsync(uri);
                }
                else if (File.Exists(att.Url))
                {
                    bytes = await File.ReadAllBytesAsync(att.Url);
                }
                else
                {
                    continue;
                }

                var ext = Path.GetExtension(
                    Uri.TryCreate(att.Url, UriKind.Absolute, out var absUri) ? absUri.AbsolutePath : att.Url);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".pdf";

                var mimeType = ext.ToLowerInvariant() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "application/pdf",
                };

                var fileName = $"{att.Label}{ext}"; // เช่น DWG.pdf, Spec.jpg, LastQuotation.pdf, ETC.pdf

                builder.Attachments.Add(fileName, bytes, ContentType.Parse(mimeType));
            }
            catch
            {
                continue;
            }
        }

        mime.Body = builder.ToMessageBody();

        using var ms = new MemoryStream();
        await mime.WriteToAsync(ms);
        var rawMessage = Convert.ToBase64String(ms.ToArray())
            .Replace('+', '-').Replace('/', '_').Replace("=", "");

        await gmail.Users.Messages.Send(new Message { Raw = rawMessage }, "me").ExecuteAsync();
    }
}