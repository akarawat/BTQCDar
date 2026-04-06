namespace BTQCDar.Controllers
{
    public class DashboardsController : BaseController
    {
        private readonly IConfiguration _config;
        private readonly IDbService _db;

        // Read directly from appsettings.json
        private string AuthenUrl => _config["TBCorApiServices:AuthenUrl"] ?? string.Empty;
        private string UrlSite => _config["TBCorApiServices:URLSITE"] ?? string.Empty;

        public DashboardsController(IConfiguration config, IDbService db)
        {
            _config = config;
            _db = db;
        }

        // ── GET /Dashboards/Index ─────────────────────────────────────────────
        public IActionResult Index(string? id, string? user, string? email,
                                   string? fname, string? depart)
        {
            // 1. Already logged in → show dashboard
            var existing = GetSession();
            //if (existing != null && !string.IsNullOrEmpty(existing.SamAcc))
            //    return View(existing);

            /*
            // 2. SSO callback — params present in query string
            if (!string.IsNullOrEmpty(user))
            {
                string[] userDomain = user.Split('\\');

                var session = new UserSessionModel
                {
                    UserId = id ?? string.Empty,
                    SamAcc = userDomain[1] ?? string.Empty,
                    Email = email ?? string.Empty,
                    FullName = fname ?? string.Empty
                    
                };

                LoadHrInfo(session);
                LoadUserRoles(session);
                SaveSession(session);
                return View(session);
            }
            */
            
            // 2. SSO for Debug
            user = "BERNINATHAILAND\\Nanthawan.C";
            id = "123456789";
            email = "Nanthawan.C@berninathailand.com";
            fname = "Nanthawan Chanthong";
            depart = "Planning, Project & IT";

            //user = "BERNINATHAILAND\\Attapol.j";
            //id = "123456789";
            //email = "Attapol.J@berninathailand.com";
            //fname = "Attapol Jingmak";
            //depart = "Planning, Project & IT";

            //user = "BERNINATHAILAND\\supaporn.t";
            //id = "123456789";
            //email = "supaporn.t@berninathailand.com";
            //fname = "Supaporn Jaidee";
            //depart = "DCC";

            if (user != "")
            {
                string[] userDomain = user.Split('\\');

                var session = new UserSessionModel
                {
                    UserId = id ?? string.Empty,
                    SamAcc = userDomain[1] ?? string.Empty,
                    Email = email ?? string.Empty,
                    FullName = fname ?? string.Empty,
                    //Dept = depart ?? string.Empty,
                };

                LoadHrInfo(session);
                LoadUserRoles(session);
                SaveSession(session);
                return View(session);
            }
            
            // 3. No session → redirect to BT SSO
            // BT SSO appends params with "&", so returnUrl must already contain "?"
            //var returnUrl = AuthenUrl;
            return Redirect(AuthenUrl);
        }

        // ── GET /Dashboards/Logout ────────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Redirect($"{AuthenUrl}/Logout");
        }

        // ── Private: Load HR info via usp_GetUserHRInfo ───────────────────────
        //
        //  SP column mapping (after ALTER):
        //    SAMACC   → SamAcc
        //    UEMAIL   → Email        (ISNULL → '')
        //    DISPNAME → FullName     (ISNULL → '')
        //    dep_code → Dep_code     (ISNULL → '')
        //    DEPART   → Department   (ISNULL → '')
        //    reporter → ManagerName  (ISNULL → '')
        //    FUNC_GetInfoByFullName(reporter,1) → ManagerSamAcc
        //    FUNC_GetInfoByFullName(reporter,2) → ManagerEmail
        //
        //  Note: SP now returns ALL users (WHERE SAMACC != '')
        //  → filter by SamAcc here in C# after reading
        // ─────────────────────────────────────────────────────────────────────
        private void LoadHrInfo(UserSessionModel session)
        {
            if (string.IsNullOrEmpty(session.SamAcc)) return;

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();

                using var cmd = new SqlCommand("dbo.usp_GetUserHRInfo", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@SamAcc", session.SamAcc);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    // SP returns all users — match by SamAcc (case-insensitive)
                    var rowSam = rdr["SamAcc"]?.ToString() ?? string.Empty;
                    if (!rowSam.Equals(session.SamAcc, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Email — prefer HR value over empty SSO callback
                    var hrEmail = rdr["Email"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(hrEmail))
                        session.Email = hrEmail;

                    // FullName — prefer SSO value (?fname=), fallback to HR DISPNAME
                    if (string.IsNullOrEmpty(session.FullName))
                        session.FullName = rdr["FullName"]?.ToString() ?? string.Empty;

                    // DepCode (numeric code e.g. "450")
                    session.DepCode = rdr["Dep_code"]?.ToString() ?? string.Empty;
                    session.DepName = rdr["Department"]?.ToString() ?? string.Empty;

                    // Manager info (via FUNC_GetInfoByFullName)
                    session.ManagerSamAcc = rdr["ManagerSamAcc"]?.ToString() ?? string.Empty;
                    session.ManagerName = rdr["ManagerName"]?.ToString() ?? string.Empty;
                    session.ManagerEmail = rdr["ManagerEmail"]?.ToString() ?? string.Empty;

                    break; // found — stop reading
                }
            }
            catch
            {
                // Non-fatal — HR lookup failure should not break login
            }
        }

        // ── Private: Load DAR role flags via usp_GetUserRoles ───────────────
        private void LoadUserRoles(UserSessionModel session)
        {
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();

                using var cmd = new SqlCommand("dbo.usp_GetUserRoles", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@SamAcc", session.SamAcc);

                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    session.IsDarApprover = (bool)(rdr["IsApprover"] ?? false);
                    session.IsMR = (bool)(rdr["IsMR"] ?? false);
                    session.IsDCO = (bool)(rdr["IsDCO"] ?? false);
                    session.IsAdmin = (bool)(rdr["IsAdmin"] ?? false);
                }
            }
            catch
            {
                // Non-fatal — roles default to requester-only
            }
        }
    }
}
