namespace BTQCDar.Controllers
{
    public class AdminController : BaseController
    {
        private readonly IDbService _db;

        public AdminController(IDbService db)
        {
            _db = db;
        }

        // ── GET /Admin/UserApprovalRoles ──────────────────────────────────────
        public IActionResult UserApprovalRoles()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsAdmin)
                return RedirectToAction("Index", "Dashboards");

            return View();
        }

        // ── GET /Admin/EmailLog — Audit trail of sent emails ──────────────────
        public IActionResult EmailLog(string? darNo, string? result, int page = 1)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsAdmin)
                return RedirectToAction("Index", "Dashboards");

            const int pageSize = 50;
            var (list, total) = GetEmailLogs(darNo, result, page, pageSize);

            ViewBag.Session = session;
            ViewBag.DarNo = darNo ?? string.Empty;
            ViewBag.Result = result ?? string.Empty;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            return View(list);
        }

        private (List<BTQCDar.Models.EmailLogModel> list, int total) GetEmailLogs(
            string? darNo, string? result, int page, int pageSize)
        {
            var list = new List<BTQCDar.Models.EmailLogModel>();
            using var conn = _db.GetQCDarConnection();
            conn.Open();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(darNo))
                where += " AND DarNo LIKE @darNo";
            if (result == "success")
                where += " AND IsSuccess = 1";
            else if (result == "failed")
                where += " AND IsSuccess = 0";

            // Count
            using (var countCmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[dar_EmailLog] {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(darNo))
                    countCmd.Parameters.AddWithValue("@darNo", $"%{darNo}%");
                var total = (int)countCmd.ExecuteScalar();

                // Page
                var sql = $@"SELECT * FROM [dbo].[dar_EmailLog] {where}
                             ORDER BY SentAt DESC
                             OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
                using var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(darNo))
                    cmd.Parameters.AddWithValue("@darNo", $"%{darNo}%");
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new BTQCDar.Models.EmailLogModel
                    {
                        LogId = (int)rdr["LogId"],
                        DarNo = rdr["DarNo"] as string,
                        ToEmail = rdr["ToEmail"].ToString()!,
                        OriginalTo = rdr["OriginalTo"] as string,
                        Subject = rdr["Subject"].ToString()!,
                        IsSuccess = (bool)rdr["IsSuccess"],
                        StatusCode = rdr["StatusCode"] as int?,
                        ResponseBody = rdr["ResponseBody"] as string,
                        ErrorMessage = rdr["ErrorMessage"] as string,
                        IsDebugMode = (bool)rdr["IsDebugMode"],
                        SentAt = (DateTime)rdr["SentAt"],
                    });
                }
                return (list, total);
            }
        }

        // ── GET /Admin/GetAllUsers (AJAX) ─────────────────────────────────────
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new List<object>());
            if (!session.IsAdmin) return Forbid();

            var list = new List<ADUserModel>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_GetAllUserFromAD", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 15
                };
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new ADUserModel
                    {
                        SamAcc = rdr["SamAcc"].ToString() ?? string.Empty,
                        Email = rdr["Email"].ToString() ?? string.Empty,
                        FullName = rdr["FullName"].ToString() ?? string.Empty,
                        DepCode = rdr["DepCode"].ToString() ?? string.Empty,
                        Department = rdr["Department"].ToString() ?? string.Empty,
                        ManagerSamAcc = rdr["ManagerSamAcc"].ToString() ?? string.Empty,
                        ManagerName = rdr["ManagerName"].ToString() ?? string.Empty,
                        ManagerEmail = rdr["ManagerEmail"].ToString() ?? string.Empty,
                        RoleType = rdr["RoleType"] != DBNull.Value ? (int)rdr["RoleType"] : null,
                        RoleName = rdr["RoleName"].ToString() ?? string.Empty,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetAllUsers] {ex.Message}");
            }
            return Json(list);
        }

        // ── GET /Admin/GetRoleConfig (AJAX) ───────────────────────────────────
        [HttpGet]
        public IActionResult GetRoleConfig()
        {
            var redirect = RequireLogin(out _);
            if (redirect != null) return Json(new List<object>());

            var list = new List<object>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT RoleType, RoleName FROM [dbo].[dar_RoleConfig] ORDER BY SortOrder", conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    list.Add(new { roleType = (int)rdr["RoleType"], roleName = rdr["RoleName"].ToString() });
            }
            catch { }
            return Json(list);
        }

        // ── POST /Admin/SaveUserRole (AJAX) ───────────────────────────────────
        [HttpPost]
        public IActionResult SaveUserRole(string samAcc, string fullName,
                                          string depCode, string depart, int roleType)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { success = false, message = "Not logged in" });
            if (!session.IsAdmin)
                return Json(new { success = false, message = "Access denied" });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_SaveUserApprovalRole", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@SamAcc", samAcc);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@DepCode", depCode);
                cmd.Parameters.AddWithValue("@Depart", depart);
                cmd.Parameters.AddWithValue("@RoleType", roleType);
                cmd.Parameters.AddWithValue("@IsActive", true);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── POST /Admin/DeleteUserRole (AJAX) ─────────────────────────────────
        [HttpPost]
        public IActionResult DeleteUserRole(int id)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { success = false });
            if (!session.IsAdmin)
                return Json(new { success = false, message = "Access denied" });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_DeleteUserApprovalRole", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── GET /Admin/GetUserRoles (AJAX) — assigned roles incl. QMRPermiss ──
        [HttpGet]
        public IActionResult GetUserRoles()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new List<object>());
            if (!session.IsAdmin) return Forbid();

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    SELECT u.Id, u.SamAcc, u.FullName, u.DepCode AS Dept,
                           u.RoleType, r.RoleName, u.IsActive,
                           ISNULL(u.QMRPermiss, 0) AS QMRPermiss
                    FROM   [dbo].[dar_UserApprovalRoles] u
                    INNER JOIN [dbo].[dar_RoleConfig] r ON r.RoleType = u.RoleType
                    ORDER BY r.SortOrder, u.FullName";
                using var cmd = new SqlCommand(sql, conn);
                using var rdr = cmd.ExecuteReader();
                var list = new List<object>();
                while (rdr.Read())
                    list.Add(new {
                        id         = (int)rdr["Id"],
                        samAcc     = rdr["SamAcc"].ToString(),
                        fullName   = rdr["FullName"].ToString(),
                        dept       = rdr["Dept"].ToString(),
                        roleType   = (int)rdr["RoleType"],
                        roleName   = rdr["RoleName"].ToString(),
                        isActive   = (bool)rdr["IsActive"],
                        qmrPermiss = (bool)rdr["QMRPermiss"]
                    });
                return Json(list);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetUserRoles] {ex.Message}");
                return Json(new List<object>());
            }
        }

        // ── POST /Admin/SaveQMRPermiss — toggle QMRPermiss for a QMR user ─────
        [HttpPost]
        public IActionResult SaveQMRPermiss(string samAcc, bool permiss)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { success = false, message = "Not logged in." });
            if (!session.IsAdmin)  return Json(new { success = false, message = "Admin only." });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    UPDATE [dbo].[dar_UserApprovalRoles]
                    SET    QMRPermiss = @Permiss, UpdatedAt = GETDATE()
                    WHERE  LOWER(SamAcc) = LOWER(@SamAcc)
                      AND  RoleType = 2";   // QMR only
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SamAcc",  samAcc);
                cmd.Parameters.AddWithValue("@Permiss", permiss);
                var rows = cmd.ExecuteNonQuery();
                return Json(new { success = rows > 0, message = rows > 0 ? "Saved." : "User not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── GET /Admin/GetQMRPermiss?samAcc=X ─────────────────────────────────
        [HttpGet]
        public IActionResult GetQMRPermiss(string samAcc)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { success = false });
            if (!session.IsAdmin)  return Json(new { success = false });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    SELECT ISNULL(QMRPermiss, 0) AS QMRPermiss
                    FROM   [dbo].[dar_UserApprovalRoles]
                    WHERE  LOWER(SamAcc) = LOWER(@SamAcc) AND RoleType = 2";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SamAcc", samAcc);
                var val = cmd.ExecuteScalar();
                return Json(new { success = true, permiss = val != null && (int)(byte)val == 1 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
