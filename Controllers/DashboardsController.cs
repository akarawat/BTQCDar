using BTQCDar.Models;
using BTQCDar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BTQCDar.Controllers
{
    public class DashboardsController : BaseController
    {
        private readonly AppSettingsModel _settings;
        private readonly IDbService _db;

        public DashboardsController(IOptions<AppSettingsModel> settings, IDbService db)
        {
            _settings = settings.Value;
            _db = db;
        }

        // ── GET /Dashboards/Index ──────────────────────────────────────────────
        public IActionResult Index(string? id, string? user, string? email,
                                   string? fname, string? depart)
        {
            // 1. Already logged in → show dashboard
            var existing = GetSession();
            if (existing != null && !string.IsNullOrEmpty(existing.SamAcc))
            {
                return View(existing);
            }

            // 2. SSO callback with parameters
            if (!string.IsNullOrEmpty(user))
            {
                var session = new UserSessionModel
                {
                    UserId = id ?? string.Empty,
                    SamAcc = user ?? string.Empty,
                    Email = email ?? string.Empty,
                    FullName = fname ?? string.Empty,
                    Dept = depart ?? string.Empty,
                };

                // Load HR info (manager) from BT_HR
                LoadHrInfo(session);

                // Load DAR roles from BT_QCDAR
                LoadUserRoles(session);

                SaveSession(session);
                return View(session);
            }

            // 3. No session → redirect to SSO
            // BT SSO appends params with "&" not "?", so returnUrl must end with "?"
            // Result: https://host/Dashboards/Index?id=...&user=...&email=...
            var returnUrl = Uri.EscapeDataString($"{_settings.URLSITE}Dashboards/Index?");
            return Redirect($"{_settings.AuthenUrl}?url={returnUrl}");
        }

        // ── GET /Dashboards/Logout ────────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Redirect(_settings.AuthenUrl + "/Logout");
        }

        // ── Private: Load manager info from BT_HR ─────────────────────────────
        private void LoadHrInfo(UserSessionModel session)
        {
            try
            {
                using var conn = _db.GetHRConnection();
                conn.Open();

                // NOTE: Adjust column names to match your actual [onl_TBADUsers] schema
                const string sql = @"
                    SELECT u.SamAccountName, u.Email, u.DisplayName,
                           m.SamAccountName AS ManagerSam,
                           m.DisplayName    AS ManagerName,
                           m.Email          AS ManagerEmail
                    FROM   [BT_HR].[dbo].[onl_TBADUsers] u
                    LEFT JOIN [BT_HR].[dbo].[onl_TBADUsers] m
                           ON u.Manager COLLATE THAI_CI_AS = m.DistinguishedName COLLATE THAI_CI_AS
                    WHERE  u.SamAccountName COLLATE THAI_CI_AS = @sam";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sam", session.SamAcc);

                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    session.ManagerSamAcc = rdr["ManagerSam"]?.ToString() ?? string.Empty;
                    session.ManagerName = rdr["ManagerName"]?.ToString() ?? string.Empty;
                    session.ManagerEmail = rdr["ManagerEmail"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // Non-fatal: HR lookup failure should not break login
            }
        }

        // ── Private: Load DAR-specific role flags from BT_QCDAR ───────────────
        private void LoadUserRoles(UserSessionModel session)
        {
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();

                const string sql = @"
                    SELECT IsApprover, IsMR, IsDCO, IsAdmin
                    FROM   [BT_QCDAR].[dbo].[dar_UserRoles]
                    WHERE  SamAcc COLLATE THAI_CI_AS = @sam";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sam", session.SamAcc);

                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    session.IsDarApprover = (bool)(rdr["IsApprover"] ?? false);
                    session.IsMR = (bool)(rdr["IsMR"] ?? false);
                    session.IsDCO = (bool)(rdr["IsDCO"] ?? false);
                    session.IsAdmin = (bool)(rdr["IsAdmin"] ?? false);
                }
                // IsDarRequester = true for everyone (default in model)
            }
            catch
            {
                // Non-fatal: roles default to requester-only if table not yet created
            }
        }
    }
}
