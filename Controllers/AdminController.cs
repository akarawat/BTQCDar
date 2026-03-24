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
                    CommandType    = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 15
                };
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new ADUserModel
                    {
                        SamAcc        = rdr["SamAcc"].ToString()        ?? string.Empty,
                        Email         = rdr["Email"].ToString()         ?? string.Empty,
                        FullName      = rdr["FullName"].ToString()      ?? string.Empty,
                        DepCode       = rdr["DepCode"].ToString()       ?? string.Empty,
                        Department    = rdr["Department"].ToString()    ?? string.Empty,
                        ManagerSamAcc = rdr["ManagerSamAcc"].ToString() ?? string.Empty,
                        ManagerName   = rdr["ManagerName"].ToString()   ?? string.Empty,
                        ManagerEmail  = rdr["ManagerEmail"].ToString()  ?? string.Empty,
                        RoleType      = rdr["RoleType"] != DBNull.Value ? (int)rdr["RoleType"] : null,
                        RoleName      = rdr["RoleName"].ToString()      ?? string.Empty,
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
                    CommandType    = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@SamAcc",   samAcc);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@DepCode",  depCode);
                cmd.Parameters.AddWithValue("@Depart",   depart);
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
                    CommandType    = System.Data.CommandType.StoredProcedure,
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
    }
}
