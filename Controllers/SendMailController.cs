using Newtonsoft.Json;
using System.Text;

namespace BTQCDar.Controllers
{
    /// <summary>
    /// Sends DAR workflow emails via BT internal mail API.
    ///
    /// EmailDebugFlag (appsettings.json → "MailSettings:DebugMode"):
    ///   false (default) = send to real recipients
    ///   true            = redirect ALL mail to DebugEmail for testing
    ///
    /// appsettings.json:
    /// "MailSettings": {
    ///   "EmailSenderApi": "https://btapi.berninathailand.com/EmailSender/EmailSender/",
    ///   "DebugMode"     : true,
    ///   "DebugEmail"    : "dev@berninathailand.com"
    /// }
    /// </summary>
    public class SendMailController : BaseController
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;

        // Read EmailSenderApi directly from MailSettings (not from TBCorApiServices)
        private string MailApiUrl =>
            _config["MailSettings:EmailSenderApi"]
            ?? "https://btapi.berninathailand.com/EmailSender/EmailSender/";

        // Debug settings — read fresh from config on every call (hot-reload friendly)
        private bool IsDebug => _config.GetValue<bool>("MailSettings:DebugMode", false);
        private string DebugEmail => _config["MailSettings:DebugEmail"] ?? string.Empty;

        public SendMailController(IHttpClientFactory http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        // ════════════════════════════════════════════════════════════════════
        // Core send method
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends a single email. Non-fatal — never throws.
        /// In DebugMode: redirects to DebugEmail and prepends [DEBUG] to subject.
        /// </summary>
        public async Task<bool> SendAsync(string toEmail,
                                          string subject,
                                          string htmlBody,
                                          string? ccEmail = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            string finalTo = toEmail;
            string finalSubject = subject;
            string finalCc = ccEmail ?? string.Empty;

            // Debug mode — redirect to debug inbox
            if (IsDebug && !string.IsNullOrWhiteSpace(DebugEmail))
            {
                finalTo = DebugEmail;
                finalCc = string.Empty;
                finalSubject = $"[DEBUG → {toEmail}] {subject}";
                Console.WriteLine($"[SendMail:DEBUG] Redirecting to {DebugEmail} | Original: {toEmail} | {subject}");
            }
            else
            {
                Console.WriteLine($"[SendMail] → {finalTo} | {finalSubject}");
            }

            try
            {
                var payload = new
                {
                    To = finalTo,
                    Cc = finalCc,
                    Subject = finalSubject,
                    Body = htmlBody,
                    IsHtml = true
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var response = await client.PostAsync(MailApiUrl, content);

                if (!response.IsSuccessStatusCode)
                    Console.Error.WriteLine($"[SendMail] API returned {(int)response.StatusCode} for {finalTo}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SendMail] Exception → {finalTo}: {ex.Message}");
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Step 1 — DAR Created → notify Reviewer
        // ════════════════════════════════════════════════════════════════════
        public Task<bool> NotifyReviewerAsync(string reviewerEmail,
                                              string darNo,
                                              string documentName,
                                              string requesterName,
                                              string requesterDept,
                                              string siteUrl)
        {
            var subject = $"[DAR] New request pending your review — {darNo}";
            var body = Build(
                title: "New DAR Pending Your Review",
                badge: ("warning", "Pending Review"),
                rows: new[]
                {
                    ("DAR No",        darNo),
                    ("Document Name", documentName),
                    ("Requested By",  requesterName),
                    ("Department",    requesterDept),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "Review Document",
                footer: "You are assigned as the Reviewer for this document action request."
            );
            return SendAsync(reviewerEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // Step 2 — Reviewed → notify Approver
        // ════════════════════════════════════════════════════════════════════
        public Task<bool> NotifyApproverAsync(string approverEmail,
                                              string darNo,
                                              string documentName,
                                              string reviewerName,
                                              string siteUrl)
        {
            var subject = $"[DAR] Reviewed — awaiting your approval — {darNo}";
            var body = Build(
                title: "DAR Ready for Your Approval",
                badge: ("info", "Pending Approval"),
                rows: new[]
                {
                    ("DAR No",        darNo),
                    ("Document Name", documentName),
                    ("Reviewed By",   reviewerName),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "Approve Document",
                footer: "You are assigned as the Approver for this document action request."
            );
            return SendAsync(approverEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // Step 3a — Approved → notify Requester (completed)
        // ════════════════════════════════════════════════════════════════════
        public Task<bool> NotifyCompletedAsync(string requesterEmail,
                                               string darNo,
                                               string documentName,
                                               string approverName,
                                               string siteUrl)
        {
            var subject = $"[DAR] Approved & Completed — {darNo}";
            var body = Build(
                title: "Your DAR Has Been Approved",
                badge: ("success", "Completed"),
                rows: new[]
                {
                    ("DAR No",        darNo),
                    ("Document Name", documentName),
                    ("Approved By",   approverName),
                    ("Date",          DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "View DAR",
                footer: "Your document action request has been fully approved."
            );
            return SendAsync(requesterEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // Step 3b — Rejected → notify Requester
        // ════════════════════════════════════════════════════════════════════
        public Task<bool> NotifyRejectedAsync(string requesterEmail,
                                              string darNo,
                                              string documentName,
                                              string rejectedByName,
                                              string remarks,
                                              string siteUrl)
        {
            var subject = $"[DAR] Not Approved — {darNo}";
            var body = Build(
                title: "Your DAR Was Not Approved",
                badge: ("danger", "Rejected"),
                rows: new[]
                {
                    ("DAR No",        darNo),
                    ("Document Name", documentName),
                    ("Rejected By",   rejectedByName),
                    ("Reason",        string.IsNullOrEmpty(remarks) ? "—" : remarks),
                },
                actionUrl: $"{siteUrl}/Dar/Detail",
                actionLabel: "View DAR",
                footer: "Please review the remarks and resubmit if necessary."
            );
            return SendAsync(requesterEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // Legacy / MR-DCO methods (backward compat)
        // ════════════════════════════════════════════════════════════════════
        public Task<bool> NotifyMRAsync(string mrEmail, string darNo,
                                        string documentName, string approverName, string siteUrl)
        {
            var subject = $"[DAR] Awaiting MR review — {darNo}";
            var body = Build(
                title: "DAR Pending MR Review",
                badge: ("info", "Pending MR"),
                rows: new[] { ("DAR No", darNo), ("Document", documentName), ("From", approverName) },
                actionUrl: $"{siteUrl}/Dar/Detail", actionLabel: "Review as MR",
                footer: "You are receiving this as Management Representative (MR)."
            );
            return SendAsync(mrEmail, subject, body);
        }

        public Task<bool> NotifyDCOAsync(string dcoEmail, string darNo,
                                         string documentName, string siteUrl)
        {
            var subject = $"[DAR] Please register document — {darNo}";
            var body = Build(
                title: "DAR Ready for DCO Registration",
                badge: ("primary", "Pending DCO"),
                rows: new[] { ("DAR No", darNo), ("Document", documentName) },
                actionUrl: $"{siteUrl}/Dar/Detail", actionLabel: "Register Document",
                footer: "You are receiving this as Document Control Officer (DCO)."
            );
            return SendAsync(dcoEmail, subject, body);
        }

        // ════════════════════════════════════════════════════════════════════
        // HTML email template builder
        // ════════════════════════════════════════════════════════════════════
        private static string Build(
            string title,
            (string color, string label) badge,
            (string label, string value)[] rows,
            string actionUrl,
            string actionLabel,
            string footer)
        {
            var rowHtml = new StringBuilder();
            foreach (var (lbl, val) in rows)
            {
                rowHtml.Append($@"
                <tr>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;
                             color:#555;font-size:13px;white-space:nowrap;width:140px;'>
                    <strong>{lbl}</strong>
                  </td>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;font-size:13px;'>
                    {System.Net.WebUtility.HtmlEncode(val)}
                  </td>
                </tr>");
            }

            var colors = new Dictionary<string, string>
            {
                ["warning"] = "#ffc107",
                ["info"] = "#0dcaf0",
                ["primary"] = "#0d6efd",
                ["success"] = "#198754",
                ["danger"] = "#dc3545",
                ["secondary"] = "#6c757d",
            };
            var bg = colors.GetValueOrDefault(badge.color, "#6c757d");

            return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'>
  <tr><td align='center' style='padding:32px 16px;'>
    <table width='620' cellpadding='0' cellspacing='0'
           style='background:#fff;border-radius:8px;overflow:hidden;
                  box-shadow:0 2px 12px rgba(0,0,0,0.10);'>

      <!-- Header -->
      <tr>
        <td style='background:#d42b2b;padding:22px 32px;'>
          <span style='color:#fff;font-size:20px;font-weight:bold;letter-spacing:-.3px;'>
            &#128196; Bernina Thailand — DAR System
          </span>
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style='padding:28px 32px 24px;'>
          <h2 style='margin:0 0 12px;color:#1a1a1a;font-size:19px;font-weight:600;'>{title}</h2>
          <span style='display:inline-block;padding:4px 16px;border-radius:20px;
                       background:{bg};color:#fff;font-size:12px;
                       font-weight:bold;margin-bottom:22px;letter-spacing:.3px;'>
            {badge.label}
          </span>
          <table width='100%' cellpadding='0' cellspacing='0'
                 style='border:1px solid #e8e8e8;border-radius:6px;
                        border-collapse:collapse;margin-bottom:26px;'>
            {rowHtml}
          </table>
          <a href='{actionUrl}'
             style='display:inline-block;padding:12px 30px;background:#d42b2b;
                    color:#fff;text-decoration:none;border-radius:6px;
                    font-weight:bold;font-size:14px;letter-spacing:.2px;'>
            {actionLabel} &#8594;
          </a>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style='padding:14px 32px;border-top:1px solid #f0f0f0;
                   color:#aaa;font-size:11px;line-height:1.6;'>
          {System.Net.WebUtility.HtmlEncode(footer)}<br>
          This is an automated notification from BTQCDar. Please do not reply to this email.
        </td>
      </tr>

    </table>
  </td></tr>
</table>
</body></html>";
        }
    }
}
