using System.Net;
using System.Net.Mail;

namespace SmartRFQ.API.Services;

public interface IEmailService
{
    Task SendQuotationRequestAsync(
        string toEmail, string rfqNo, string requester, string purchaser, List<string> attachmentPaths);
}

public class EmailService(IConfiguration cfg, IHttpClientFactory httpClientFactory) : IEmailService
{
    public async Task SendQuotationRequestAsync(
        string toEmail, string rfqNo, string requester, string purchaser, List<string> attachmentPaths)
    {
        var host     = cfg["Email:Host"]!;
        var port     = int.Parse(cfg["Email:Port"] ?? "587");
        var user     = cfg["Email:Username"]!;
        var pass     = cfg["Email:Password"]!;
        var fromName = cfg["Email:FromName"] ?? "Smart RFQ System";

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl   = true,
        };

        var mail = new MailMessage
        {
            From       = new MailAddress(user, fromName),
            Subject    = $"[Smart RFQ] ขอใบเสนอราคา {rfqNo}",
            IsBodyHtml = true,
            Body       = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto">
                  <h2 style="color:#1d4ed8">📄 คำขอใบเสนอราคา</h2>
                  <p>เรียน Vendor,</p>
                  <p>บริษัทมีความประสงค์ขอใบเสนอราคาสำหรับ RFQ หมายเลข <strong>{rfqNo}</strong></p>
                  <table style="border-collapse:collapse;width:100%;margin:16px 0">
                    <tr style="background:#f1f5f9">
                      <td style="padding:8px 12px;font-weight:600">RFQ No.</td>
                      <td style="padding:8px 12px">{rfqNo}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 12px;font-weight:600">ผู้ขอ</td>
                      <td style="padding:8px 12px">{requester}</td>
                    </tr>
                    <tr style="background:#f1f5f9">
                      <td style="padding:8px 12px;font-weight:600">ผู้ดูแล (Purchase)</td>
                      <td style="padding:8px 12px">{purchaser}</td>
                    </tr>
                  </table>
                  <p>กรุณาส่งใบเสนอราคากลับมาที่ <a href="mailto:{purchaser}">{purchaser}</a></p>
                  <hr style="border:none;border-top:1px solid #e2e8f0;margin:24px 0"/>
                  <p style="color:#94a3b8;font-size:12px">Smart RFQ System — อีเมลนี้ส่งโดยระบบอัตโนมัติ</p>
                </div>
                """
        };
        mail.To.Add(toEmail);
       
        var streams = new List<Stream>();     
        var http = httpClientFactory.CreateClient();

        foreach (var path in attachmentPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try
            {
                byte[] bytes;

                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    bytes = await http.GetByteArrayAsync(uri);
                }
                else if (File.Exists(path))
                {
                    bytes = await File.ReadAllBytesAsync(path);
                }
                else
                {
                    continue; 
                }

                var ms = new MemoryStream(bytes);
                streams.Add(ms);

                var fileName = Path.GetFileName(new Uri(path, UriKind.RelativeOrAbsolute).IsAbsoluteUri
                    ? new Uri(path).AbsolutePath
                    : path);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "quotation.pdf";

                var attachment = new Attachment(ms, fileName, "application/pdf");
                mail.Attachments.Add(attachment);
            }
            catch
            {
                
                continue;
            }
        }

        try
        {
            await client.SendMailAsync(mail);
        }
        finally
        {
            mail.Dispose();
            foreach (var s in streams) s.Dispose();
        }
    }
}