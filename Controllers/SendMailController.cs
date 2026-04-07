using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BTQCDar.Controllers
{
    public class SendMailController : BaseController
    {
        private readonly IConfiguration _config;

        // Read from appsettings.json — same keys as the working MailSenderMessage project
        private string MailApiUrl => _config["TBCorApiServices:EmailSender"] ?? string.Empty;
        private string MailForm => _config["TBCorApiServices:MailForm"] ?? string.Empty;
        private string BearerToken => _config["TBCorApiServices:MailToken"] ?? "-dev_token-";

        // Debug redirect
        private bool IsDebug => _config.GetValue<bool>("MailSettings:DebugMode", false);
        private string DebugEmail => _config["MailSettings:DebugEmail"] ?? string.Empty;

        public SendMailController(IConfiguration config)
        {
            _config = config;
        }

        // ── Core send — mirrors the working MailSenderMessage exactly ─────────
        public async Task<bool> SendAsync(string toEmail, string subject,
                                          string htmlBody, string? ccEmail = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;
            if (string.IsNullOrWhiteSpace(MailApiUrl))
            {
                Console.Error.WriteLine("[SendMail] TBCorApiServices:EmailSender is not configured.");
                return false;
            }

            // Debug mode — redirect to DebugEmail
            string finalTo = IsDebug && !string.IsNullOrWhiteSpace(DebugEmail) ? DebugEmail : toEmail;
            string finalSubject = IsDebug ? $"[DEBUG → {toEmail}] {subject}" : subject;

            Console.WriteLine(IsDebug
                ? $"[SendMail:DEBUG] {toEmail} → {finalTo} | {subject}"
                : $"[SendMail] → {finalTo} | {subject}");

            try
            {
                // Payload matches SenderMailModel exactly
                var param = new
                {
                    body = htmlBody,
                    form = MailForm,
                    subject = finalSubject,
                    addresses = finalTo,   // single string, not array
                    priority = 1
                };

                var json = JsonSerializer.Serialize(param);
                var strContent = new StringContent(json, Encoding.UTF8, "application/json");

                // HttpClientHandler — mirrors the working project
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = new CookieContainer(),
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + BearerToken);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.PostAsync(MailApiUrl, strContent);
                var body = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[SendMail] Status={(int)response.StatusCode} Response={body.Substring(0, Math.Min(200, body.Length))}");

                if (!response.IsSuccessStatusCode)
                    Console.Error.WriteLine($"[SendMail] FAILED {(int)response.StatusCode} to {finalTo}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SendMail] Exception → {finalTo}: {ex.Message}");
                return false;
            }
        }

        // ── Notification templates ────────────────────────────────────────────

        public Task<bool> NotifyReviewerAsync(string reviewerEmail, string darNo,
            string documentName, string requesterName, string requesterDept,
            string siteUrl, int darId)
            => SendAsync(reviewerEmail, $"[DAR] New request pending your review — {darNo}",
                Build("New DAR Pending Your Review", ("warning", "Pending Review"), new[] {
                    ("DAR No", darNo), ("Document Name", documentName),
                    ("Requested By", requesterName), ("Department", requesterDept) },
                    $"{siteUrl}/Dar/Detail/{darId}", "Review Document",
                    "You are assigned as the Reviewer for this document action request."));

        public Task<bool> NotifyApproverAsync(string approverEmail, string darNo,
            string documentName, string reviewerName, string siteUrl, int darId)
            => SendAsync(approverEmail, $"[DAR] Reviewed — awaiting your approval — {darNo}",
                Build("DAR Ready for Your Approval", ("info", "Pending Approval"), new[] {
                    ("DAR No", darNo), ("Document Name", documentName),
                    ("Reviewed By", reviewerName) },
                    $"{siteUrl}/Dar/Detail/{darId}", "Approve Document",
                    "You are assigned as the Approver for this document action request."));

        public Task<bool> NotifyCompletedAsync(string requesterEmail, string darNo,
            string documentName, string approverName, string siteUrl, int darId)
            => SendAsync(requesterEmail, $"[DAR] Approved & Completed — {darNo}",
                Build("Your DAR Has Been Approved", ("success", "Completed"), new[] {
                    ("DAR No", darNo), ("Document Name", documentName),
                    ("Approved By", approverName),
                    ("Date", DateTime.Now.ToString("dd/MM/yyyy HH:mm")) },
                    $"{siteUrl}/Dar/Detail/{darId}", "View DAR",
                    "Your document action request has been fully approved."));

        public Task<bool> NotifyRejectedAsync(string requesterEmail, string darNo,
            string documentName, string rejectedByName, string remarks,
            string siteUrl, int darId)
            => SendAsync(requesterEmail, $"[DAR] Not Approved — {darNo}",
                Build("Your DAR Was Not Approved", ("danger", "Rejected"), new[] {
                    ("DAR No", darNo), ("Document Name", documentName),
                    ("Rejected By", rejectedByName),
                    ("Reason", string.IsNullOrEmpty(remarks) ? "—" : remarks) },
                    $"{siteUrl}/Dar/Detail/{darId}", "View DAR",
                    "Please review the remarks and resubmit if necessary."));

        public Task<bool> NotifyMRAsync(string mrEmail, string darNo,
            string documentName, string approverName, string siteUrl, int darId)
            => SendAsync(mrEmail, $"[DAR] Awaiting QMR review — {darNo}",
                Build("DAR Pending QMR Review", ("info", "Pending QMR"), new[] {
                    ("DAR No", darNo), ("Document", documentName), ("Approved By", approverName) },
                    $"{siteUrl}/Dar/Detail/{darId}", "Review as QMR",
                    "You are receiving this as Management Representative (QMR)."));

        public Task<bool> NotifyDCOAsync(string dcoEmail, string darNo,
            string documentName, string siteUrl, int darId)
            => SendAsync(dcoEmail, $"[DAR] Please register document — {darNo}",
                Build("DAR Ready for DCC Registration", ("primary", "Pending DCC"), new[] {
                    ("DAR No", darNo), ("Document", documentName) },
                    $"{siteUrl}/Dar/Detail/{darId}", "Register Document",
                    "You are receiving this as Document Control Officer (DCC)."));


        // ── HTML builder ──────────────────────────────────────────────────────
        private static string Build(string title, (string color, string label) badge,
            (string label, string value)[] rows, string actionUrl,
            string actionLabel, string footer)
        {
            var rowHtml = new StringBuilder();
            foreach (var (lbl, val) in rows)
                rowHtml.Append($@"<tr>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;color:#555;font-size:13px;white-space:nowrap;width:140px;'><strong>{lbl}</strong></td>
                  <td style='padding:8px 14px;border-bottom:1px solid #f0f0f0;font-size:13px;'>{System.Net.WebUtility.HtmlEncode(val)}</td></tr>");

            var colors = new Dictionary<string, string>
            {
                ["warning"] = "#ffc107",
                ["info"] = "#0dcaf0",
                ["primary"] = "#0d6efd",
                ["success"] = "#198754",
                ["danger"] = "#dc3545"
            };

            return $@"<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0'><tr><td align='center' style='padding:32px 16px;'>
<table width='620' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);'>
<tr><td style='background:#1a56db;padding:22px 32px;'><span style='color:#fff;font-size:20px;font-weight:bold;'>&#128196; Bernina Thailand — DAR System</span></td></tr>
<tr><td style='padding:28px 32px 24px;'>
  <h2 style='margin:0 0 12px;color:#1a1a1a;font-size:19px;'>{title}</h2>
  <span style='display:inline-block;padding:4px 16px;border-radius:20px;background:{colors.GetValueOrDefault(badge.color, "#6c757d")};color:#fff;font-size:12px;font-weight:bold;margin-bottom:22px;'>{badge.label}</span>
  <table width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e8e8e8;border-radius:6px;border-collapse:collapse;margin-bottom:26px;'>{rowHtml}</table>
  <a href='{actionUrl}' style='display:inline-block;padding:12px 30px;background:#1a56db;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;font-size:14px;'>{actionLabel} &#8594;</a>
</td></tr>
<tr><td style='padding:14px 32px;border-top:1px solid #f0f0f0;color:#aaa;font-size:11px;line-height:1.6;'>
  {System.Net.WebUtility.HtmlEncode(footer)}<br>This is an automated notification from BTQCDar. Please do not reply.
</td></tr></table></td></tr></table></body></html>";
        }
    }
}
