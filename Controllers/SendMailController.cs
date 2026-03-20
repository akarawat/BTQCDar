using BTQCDar.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace BTQCDar.Controllers
{
    /// <summary>
    /// Sends email via the BT internal mail API (TBCorApiServices).
    /// Endpoint: POST {URLSITE}/SendMail/MailSenderMessage
    ///
    /// Usage from other controllers:
    ///   await _mailer.SendAsync(to, subject, htmlBody);
    /// </summary>
    public class SendMailController : BaseController
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettingsModel _settings;

        public SendMailController(IHttpClientFactory httpClientFactory,
                                  IOptions<AppSettingsModel> settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
        }

        // ── Endpoint URL (same pattern as your RequestController) ────────────
        private string LocalMailUrl =>
            $"{_settings.URLSITE}/SendMail/MailSenderMessage";

        // ════════════════════════════════════════════════════════════════════
        // Public helper — call this from DarController after status changes
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Send a single email via BT mail API.
        /// Returns true if the API responded 2xx, false otherwise (non-fatal).
        /// </summary>
        public async Task<bool> SendAsync(string toEmail,
                                          string subject,
                                          string htmlBody,
                                          string? ccEmail = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            try
            {
                var payload = new
                {
                    To = toEmail,
                    Cc = ccEmail ?? string.Empty,
                    Subject = subject,
                    Body = htmlBody,
                    IsHtml = true
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(LocalMailUrl, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Non-fatal — log and continue; mail failure should not break workflow
                Console.Error.WriteLine($"[SendMail] Error sending to {toEmail}: {ex.Message}");
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Pre-built DAR email templates
        // ════════════════════════════════════════════════════════════════════

        /// <summary>DAR submitted — notify Approver</summary>
        public Task<bool> NotifyApproverAsync(string approverEmail,
                                               string darNo,
                                               string documentName,
                                               string requesterName,
                                               string siteUrl)
        {
            var subject = $"[DAR] New request awaiting your approval — {darNo}";
            var body = BuildTemplate(
                title: "New DAR Awaiting Approval",
                badge: ("warning", "Pending Approval"),
                rows: new[]
                {
                    ("DAR No",        darNo),
                    ("Document Name", documentName),
                    ("Requested By",  requesterName),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "Review & Approve",
                footer: "You are receiving this email because you are an Approver in the DAR system."
            );
            return SendAsync(approverEmail, subject, body);
        }

        /// <summary>Approved — notify MR</summary>
        public Task<bool> NotifyMRAsync(string mrEmail,
                                        string darNo,
                                        string documentName,
                                        string approverName,
                                        string siteUrl)
        {
            var subject = $"[DAR] Approved — awaiting your review as MR — {darNo}";
            var body = BuildTemplate(
                title: "DAR Approved — MR Review Required",
                badge: ("info", "Pending MR"),
                rows: new[]
                {
                    ("DAR No",       darNo),
                    ("Document",     documentName),
                    ("Approved By",  approverName),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "Review as MR",
                footer: "You are receiving this email as Management Representative (MR)."
            );
            return SendAsync(mrEmail, subject, body);
        }

        /// <summary>MR agreed — notify DCO</summary>
        public Task<bool> NotifyDCOAsync(string dcoEmail,
                                         string darNo,
                                         string documentName,
                                         string siteUrl)
        {
            var subject = $"[DAR] MR Approved — please register document — {darNo}";
            var body = BuildTemplate(
                title: "DAR Ready for DCO Registration",
                badge: ("primary", "Pending DCO"),
                rows: new[]
                {
                    ("DAR No",   darNo),
                    ("Document", documentName),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "Register Document",
                footer: "You are receiving this email as Document Control Officer (DCO)."
            );
            return SendAsync(dcoEmail, subject, body);
        }

        /// <summary>DAR completed — notify requester</summary>
        public Task<bool> NotifyCompletedAsync(string requesterEmail,
                                               string darNo,
                                               string documentName,
                                               string siteUrl)
        {
            var subject = $"[DAR] Completed — {darNo}";
            var body = BuildTemplate(
                title: "DAR Successfully Completed",
                badge: ("success", "Completed"),
                rows: new[]
                {
                    ("DAR No",   darNo),
                    ("Document", documentName),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "View DAR",
                footer: "Your document action request has been fully processed."
            );
            return SendAsync(requesterEmail, subject, body);
        }

        /// <summary>DAR rejected — notify requester</summary>
        public Task<bool> NotifyRejectedAsync(string requesterEmail,
                                              string darNo,
                                              string documentName,
                                              string remarks,
                                              string siteUrl)
        {
            var subject = $"[DAR] Not Approved — {darNo}";
            var body = BuildTemplate(
                title: "DAR Was Not Approved",
                badge: ("danger", "Rejected"),
                rows: new[]
                {
                    ("DAR No",   darNo),
                    ("Document", documentName),
                    ("Reason",   string.IsNullOrEmpty(remarks) ? "—" : remarks),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "View DAR",
                footer: "Please review the remarks and resubmit if necessary."
            );
            return SendAsync(requesterEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // HTML template builder
        // ════════════════════════════════════════════════════════════════════

        private static string BuildTemplate(
            string title,
            (string color, string label) badge,
            (string label, string value)[] rows,
            string actionUrl,
            string actionLabel,
            string footer)
        {
            var rowsHtml = new StringBuilder();
            foreach (var (label, value) in rows)
            {
                rowsHtml.Append($@"
                <tr>
                    <td style='padding:8px 12px;border-bottom:1px solid #f0f0f0;
                               color:#666;font-size:13px;white-space:nowrap;
                               width:140px;'><strong>{label}</strong></td>
                    <td style='padding:8px 12px;border-bottom:1px solid #f0f0f0;
                               font-size:13px;'>{System.Net.WebUtility.HtmlEncode(value)}</td>
                </tr>");
            }

            var badgeColors = new Dictionary<string, string>
            {
                ["warning"] = "#ffc107",
                ["info"] = "#0dcaf0",
                ["primary"] = "#0d6efd",
                ["success"] = "#198754",
                ["danger"] = "#dc3545",
            };
            var bgColor = badgeColors.GetValueOrDefault(badge.color, "#6c757d");

            return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0'>
    <tr><td align='center' style='padding:32px 16px;'>
      <table width='600' cellpadding='0' cellspacing='0'
             style='background:#fff;border-radius:8px;overflow:hidden;
                    box-shadow:0 2px 8px rgba(0,0,0,0.08);'>

        <!-- Header -->
        <tr>
          <td style='background:#d42b2b;padding:24px 32px;'>
            <span style='color:#fff;font-size:20px;font-weight:bold;'>
              &#128196; Bernina Thailand — DAR System
            </span>
          </td>
        </tr>

        <!-- Body -->
        <tr>
          <td style='padding:28px 32px;'>
            <h2 style='margin:0 0 16px;color:#1a1a1a;font-size:18px;'>{title}</h2>
            <span style='display:inline-block;padding:4px 14px;border-radius:20px;
                         background:{bgColor};color:#fff;font-size:12px;
                         font-weight:bold;margin-bottom:20px;'>
              {badge.label}
            </span>

            <table width='100%' cellpadding='0' cellspacing='0'
                   style='border:1px solid #e8e8e8;border-radius:6px;
                          border-collapse:collapse;margin-bottom:24px;'>
              {rowsHtml}
            </table>

            <a href='{actionUrl}'
               style='display:inline-block;padding:12px 28px;background:#d42b2b;
                      color:#fff;text-decoration:none;border-radius:6px;
                      font-weight:bold;font-size:14px;'>
              {actionLabel} &#8594;
            </a>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='padding:16px 32px;border-top:1px solid #f0f0f0;
                     color:#999;font-size:11px;'>
            {footer}<br>
            This is an automated message from the BTQCDar system. Please do not reply.
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
