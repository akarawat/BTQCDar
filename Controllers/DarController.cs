namespace BTQCDar.Controllers
{
    public class DarController : BaseController
    {
        private readonly IDbService _db;
        private readonly IWebHostEnvironment _env;
        private readonly SendMailController _mailer;
        private readonly AppSettingsModel _appSettings;

        public DarController(IDbService db,
                             IWebHostEnvironment env,
                             SendMailController mailer,
                             IOptions<AppSettingsModel> settings)
        {
            _db = db;
            _env = env;
            _mailer = mailer;
            _appSettings = settings.Value;
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Index  — list my DARs
        // ────────────────────────────────────────────────────────────────────
        public IActionResult Index()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            var list = GetDarList(session);
            ViewBag.Session = session;
            return View(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Create  — new DAR form
        // ────────────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            var model = new DarMasterModel
            {
                RequestedBySamAcc = session.SamAcc,
                RequestedByName = session.FullName,
                RequestedDate = DateTime.Now,
            };

            ViewBag.Session = session;
            return View(model);
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/Create
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DarMasterModel model, IFormFile? attachmentFile)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null)
                return Json(new { success = false, message = "Session expired. Please login again." });

            // Skip ModelState — we do manual validation below.
            // ASP.NET 8 nullable context marks every non-nullable string as [Required]
            // which causes false failures for optional fields (RevisionNo, DistributionList, etc.)
            ModelState.Clear();

            // Manual required-field check
            var errors = new List<string>();
            if (model.DocType == 0) errors.Add("Please select a document type.");
            if (model.ForStandard == 0) errors.Add("Please select a standard (For).");
            if (model.Purpose == 0) errors.Add("Please select a purpose.");
            if (string.IsNullOrWhiteSpace(model.DocumentName))
                errors.Add("Document Name is required.");
            if (string.IsNullOrWhiteSpace(model.Content))
                errors.Add("Content is required.");
            if (string.IsNullOrWhiteSpace(model.ReviewerSamAcc))
                errors.Add("Please select a Reviewer.");

            if (errors.Any())
                return Json(new { success = false, message = string.Join(" | ", errors) });

            try
            {
                model.RequestedBySamAcc = session.SamAcc;
                model.RequestedByName = session.FullName;
                model.RequestedDate = DateTime.Now;
                model.Status = DarStatus.PendingApproval;
                model.DarNo = GenerateDarNo();

                // Handle file upload
                if (attachmentFile != null && attachmentFile.Length > 0)
                {
                    model.HasAttachment = true;
                    model.AttachmentFileName = await SaveAttachment(attachmentFile, model.DarNo);
                }

                int newId = InsertDar(model);

                // Notify Reviewer — fire-and-forget
                if (!string.IsNullOrEmpty(model.ReviewerEmail))
                    _ = _mailer.NotifyApproverAsync(
                            model.ReviewerEmail,
                            model.DarNo, model.DocumentName,
                            model.RequestedByName, _appSettings.URLSITE);

                return Json(new
                {
                    success = true,
                    darNo = model.DarNo,
                    darId = newId,
                    redirect = Url.Action("Detail", "Dar", new { id = newId })
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Create DAR] {ex.Message}");
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Detail/{id}
        // ────────────────────────────────────────────────────────────────────
        public IActionResult Detail(int id)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            var model = GetDarById(id);
            if (model == null) return NotFound();

            ViewBag.Session = session;
            return View(model);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Edit/{id}
        // ────────────────────────────────────────────────────────────────────
        public IActionResult Edit(int id)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            var model = GetDarById(id);
            if (model == null) return NotFound();

            // Only requester (while Draft) or Admin can edit
            if (model.Status != DarStatus.Draft && !session.IsAdmin)
            {
                TempData["Error"] = "Cannot edit — this DAR is already in progress.";
                return RedirectToAction("Detail", new { id });
            }

            ViewBag.Session = session;
            return View(model);
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/Edit/{id}
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, DarMasterModel model)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            if (!ModelState.IsValid)
            {
                ViewBag.Session = session;
                return View(model);
            }

            UpdateDar(model);
            TempData["Success"] = "Changes saved successfully.";
            return RedirectToAction("Detail", new { id });
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/Approve  — Approver approves
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Approve(int id, string? remarks)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsDarApprover && !session.IsAdmin)
                return Forbid();

            UpdateStatus(id, DarStatus.PendingMR,
                         approvedBySam: session.SamAcc,
                         approvedByName: session.FullName,
                         approvedDate: DateTime.Now,
                         remarks: remarks);

            TempData["Success"] = "Approved — forwarded to MR.";

            var dar = GetDarById(id);
            if (dar != null)
                _ = _mailer.NotifyMRAsync(
                        GetMREmail(),
                        dar.DarNo, dar.DocumentName,
                        session.FullName, _appSettings.URLSITE);

            return RedirectToAction("Detail", new { id });
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/MRAgree
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult MRAgree(int id, bool agree, string? remarks)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsMR && !session.IsAdmin) return Forbid();

            var nextStatus = agree ? DarStatus.PendingDCO : DarStatus.Rejected;
            UpdateMR(id, agree, session.SamAcc, DateTime.Now, nextStatus, remarks);

            TempData["Success"] = agree ? "MR Agreed — forwarded to DCO." : "MR did not agree.";

            var darMR = GetDarById(id);
            if (darMR != null)
            {
                if (agree)
                    _ = _mailer.NotifyDCOAsync(
                            GetDCOEmail(),
                            darMR.DarNo, darMR.DocumentName,
                            _appSettings.URLSITE);
                else
                    _ = _mailer.NotifyRejectedAsync(
                            GetRequesterEmail(darMR.RequestedBySamAcc),
                            darMR.DarNo, darMR.DocumentName,
                            "", _appSettings.URLSITE);
            }

            return RedirectToAction("Detail", new { id });
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/DCORegister
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult DCORegister(int id, DateTime registeredDate, string? remarks)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsDCO && !session.IsAdmin) return Forbid();

            UpdateDCO(id, session.SamAcc, registeredDate, DarStatus.Completed, remarks);

            TempData["Success"] = "Document registered by DCO — DAR completed.";

            var darDCO = GetDarById(id);
            if (darDCO != null)
                _ = _mailer.NotifyCompletedAsync(
                        GetRequesterEmail(darDCO.RequestedBySamAcc),
                        darDCO.DarNo, darDCO.DocumentName,
                        _appSettings.URLSITE);

            return RedirectToAction("Detail", new { id });
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/Reject
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Reject(int id, string? remarks)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;
            if (!session.IsDarApprover && !session.IsMR && !session.IsAdmin)
                return Forbid();

            UpdateStatus(id, DarStatus.Rejected, remarks: remarks);
            TempData["Error"] = "DAR has been rejected.";

            var darRej = GetDarById(id);
            if (darRej != null)
                _ = _mailer.NotifyRejectedAsync(
                        GetRequesterEmail(darRej.RequestedBySamAcc),
                        darRej.DarNo, darRej.DocumentName,
                        remarks ?? "", _appSettings.URLSITE);

            return RedirectToAction("Detail", new { id });
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Pending  — inbox for approver / MR / DCO
        // ────────────────────────────────────────────────────────────────────
        public IActionResult Pending()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            var list = GetPendingList(session);
            ViewBag.Session = session;
            return View(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/GetApprovalUsers
        // Returns users from dar_UserApprovalRoles (RoleType 1-6) as JSON
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetApprovalUsers()
        {
            var redirect = RequireLogin(out _);
            if (redirect != null) return Json(new List<object>());

            var list = new List<DarApproverOptionModel>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    SELECT  u.SamAcc, u.FullName, u.DepCode, u.Depart,
                            u.RoleType, r.RoleName
                    FROM    [dbo].[dar_UserApprovalRoles] u
                    JOIN    [dbo].[dar_RoleConfig]        r ON r.RoleType = u.RoleType
                    WHERE   u.IsActive = 1
                      AND   u.RoleType BETWEEN 1 AND 6
                    ORDER BY u.Depart, r.SortOrder, u.FullName";

                using var cmd = new SqlCommand(sql, conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new DarApproverOptionModel
                    {
                        SamAcc = rdr["SamAcc"].ToString()!,
                        FullName = rdr["FullName"].ToString()!,
                        DepCode = rdr["DepCode"].ToString()!,
                        Depart = rdr["Depart"].ToString()!,
                        RoleType = (int)rdr["RoleType"],
                        RoleName = rdr["RoleName"].ToString()!,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetApprovalUsers] {ex.Message}");
            }
            return Json(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/GetDarApprovers/{darId}  — for Detail page
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetDarApprovers(int darId)
        {
            var redirect = RequireLogin(out _);
            if (redirect != null) return Json(new List<object>());

            var list = new List<DarApproverModel>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    SELECT  a.Id, a.DarId, a.SamAcc, a.FullName,
                            a.DepCode, a.Depart, a.RoleType, r.RoleName, a.SortOrder
                    FROM    [dbo].[dar_DarApprovers] a
                    JOIN    [dbo].[dar_RoleConfig]   r ON r.RoleType = a.RoleType
                    WHERE   a.DarId = @darId
                    ORDER BY a.SortOrder, r.SortOrder";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@darId", darId);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new DarApproverModel
                    {
                        Id = (int)rdr["Id"],
                        DarId = (int)rdr["DarId"],
                        SamAcc = rdr["SamAcc"].ToString()!,
                        FullName = rdr["FullName"].ToString()!,
                        DepCode = rdr["DepCode"].ToString()!,
                        Depart = rdr["Depart"].ToString()!,
                        RoleType = (int)rdr["RoleType"],
                        RoleName = rdr["RoleName"].ToString()!,
                        SortOrder = (int)rdr["SortOrder"],
                    });
                }
            }
            catch { }
            return Json(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/GetReviewers?docType=1
        // Returns eligible reviewers for a DocType via SP
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetReviewers(int docType)
        {
            var redirect = RequireLogin(out _);
            if (redirect != null) return Json(new List<object>());

            var list = new List<UserDropdownModel>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_GetReviewersByDocType", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@DocType", docType);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    list.Add(MapUserDropdown(rdr));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetReviewers] {ex.Message}");
            }
            return Json(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/GetApprover?docType=1
        // Returns fixed approver(s) for a DocType via SP
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetApprover(int docType)
        {
            var redirect = RequireLogin(out _);
            if (redirect != null) return Json(new List<object>());

            var list = new List<UserDropdownModel>();
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_GetApproverByDocType", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@DocType", docType);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    list.Add(MapUserDropdown(rdr));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GetApprover] {ex.Message}");
            }
            return Json(list);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/CheckCreatorPermission?docType=1
        // Returns { isAllowed: true/false }
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult CheckCreatorPermission(int docType)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { isAllowed = false });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                using var cmd = new SqlCommand("dbo.usp_CheckCreatorPermission", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    CommandTimeout = 10
                };
                cmd.Parameters.AddWithValue("@DocType", docType);
                cmd.Parameters.AddWithValue("@SamAcc", session.SamAcc);
                var result = cmd.ExecuteScalar();
                return Json(new { isAllowed = (result != null && (bool)result) });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CheckCreatorPermission] {ex.Message}");
                return Json(new { isAllowed = false });
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // GET /Dar/Stats  — JSON stats for dashboard widget
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Stats()
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return Json(new { });

            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                var sql = session.IsAdmin
                    ? @"SELECT
                            SUM(CASE WHEN Status=0 THEN 1 ELSE 0 END) AS draft,
                            SUM(CASE WHEN Status IN (1,2,3) THEN 1 ELSE 0 END) AS pending,
                            SUM(CASE WHEN Status=4 THEN 1 ELSE 0 END) AS completed,
                            SUM(CASE WHEN Status=5 THEN 1 ELSE 0 END) AS rejected
                       FROM [dbo].[dar_Master]"
                    : @"SELECT
                            SUM(CASE WHEN Status=0 THEN 1 ELSE 0 END) AS draft,
                            SUM(CASE WHEN Status IN (1,2,3) THEN 1 ELSE 0 END) AS pending,
                            SUM(CASE WHEN Status=4 THEN 1 ELSE 0 END) AS completed,
                            SUM(CASE WHEN Status=5 THEN 1 ELSE 0 END) AS rejected
                       FROM [dbo].[dar_Master]
                       WHERE RequestedBySamAcc=@sam";

                using var cmd = new SqlCommand(sql, conn);
                if (!session.IsAdmin)
                    cmd.Parameters.AddWithValue("@sam", session.SamAcc);

                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                    return Json(new
                    {
                        draft = rdr["draft"],
                        pending = rdr["pending"],
                        completed = rdr["completed"],
                        rejected = rdr["rejected"]
                    });
            }
            catch { /* Return zeros on error */ }

            return Json(new { draft = 0, pending = 0, completed = 0, rejected = 0 });
        }

        // ══════════════════════════════════════════════════════════════════
        // DATA ACCESS  (raw ADO.NET — no EF, consistent with BTTemplate)
        // ══════════════════════════════════════════════════════════════════

        // ────────────────────────────────────────────────────────────────────
        // Email helper methods
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Notify all users flagged as Approver in dar_UserRoles</summary>
        private async Task NotifyApproversAsync(string darNo, string documentName, string requesterName)
        {
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = "SELECT SamAcc, FullName FROM [dbo].[dar_UserRoles] WHERE IsApprover=1";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                using var rdr = cmd.ExecuteReader();
                var tasks = new List<Task>();
                while (rdr.Read())
                {
                    var email = GetRequesterEmail(rdr["SamAcc"].ToString()!);
                    if (!string.IsNullOrEmpty(email))
                        tasks.Add(_mailer.NotifyApproverAsync(
                            email, darNo, documentName, requesterName, _appSettings.URLSITE));
                }
                await Task.WhenAll(tasks);
            }
            catch { /* non-fatal */ }
        }

        /// <summary>Get email from BT_HR by SamAcc</summary>
        private string GetRequesterEmail(string samAcc)
        {
            if (string.IsNullOrEmpty(samAcc)) return string.Empty;
            try
            {
                using var conn = _db.GetHRConnection();
                conn.Open();
                const string sql = "SELECT TOP 1 Email FROM [dbo].[onl_TBADUsers] WHERE SamAccountName COLLATE THAI_CI_AS = @sam";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sam", samAcc);
                return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Get first MR email from dar_UserRoles</summary>
        private string GetMREmail()
        {
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = "SELECT TOP 1 SamAcc FROM [dbo].[dar_UserRoles] WHERE IsMR=1";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                var sam = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                return GetRequesterEmail(sam);
            }
            catch { return string.Empty; }
        }

        /// <summary>Get first DCO email from dar_UserRoles</summary>
        private string GetDCOEmail()
        {
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = "SELECT TOP 1 SamAcc FROM [dbo].[dar_UserRoles] WHERE IsDCO=1";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                var sam = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                return GetRequesterEmail(sam);
            }
            catch { return string.Empty; }
        }

        // ────────────────────────────────────────────────────────────────────
        // File Upload Helper
        // ────────────────────────────────────────────────────────────────────
        private async Task<string> SaveAttachment(IFormFile file, string darNo)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadDir); // ensure folder exists

            // Sanitize: keep original extension, prefix with DarNo + timestamp
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeName = $"{darNo}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var fullPath = Path.Combine(uploadDir, safeName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return safeName; // store only filename, not full path
        }


        private void SaveApprovers(int darId, List<string> selectedKeys)
        {
            // selectedKeys format: "SamAcc|DepCode|RoleType"
            if (selectedKeys == null || !selectedKeys.Any()) return;

            using var conn = _db.GetQCDarConnection();
            conn.Open();

            // Delete existing (replace all)
            using var del = new SqlCommand(
                "DELETE FROM [dbo].[dar_DarApprovers] WHERE DarId=@darId", conn);
            del.Parameters.AddWithValue("@darId", darId);
            del.ExecuteNonQuery();

            int order = 1;
            foreach (var key in selectedKeys)
            {
                var parts = key.Split('|');
                if (parts.Length < 3) continue;
                var samAcc = parts[0].Trim();
                var depCode = parts[1].Trim();
                if (!int.TryParse(parts[2].Trim(), out int roleType)) continue;

                // Look up display info
                string fullName = "", depart = "";
                using var look = new SqlCommand(@"
                    SELECT TOP 1 FullName, Depart
                    FROM [dbo].[dar_UserApprovalRoles]
                    WHERE SamAcc=@sam AND DepCode=@dep AND RoleType=@rt", conn);
                look.Parameters.AddWithValue("@sam", samAcc);
                look.Parameters.AddWithValue("@dep", depCode);
                look.Parameters.AddWithValue("@rt", roleType);
                using var lr = look.ExecuteReader();
                if (lr.Read())
                {
                    fullName = lr["FullName"].ToString()!;
                    depart = lr["Depart"].ToString()!;
                }
                lr.Close();

                using var ins = new SqlCommand(@"
                    INSERT INTO [dbo].[dar_DarApprovers]
                        (DarId, SamAcc, FullName, DepCode, Depart, RoleType, SortOrder)
                    VALUES (@darId, @sam, @name, @dep, @depart, @rt, @ord)", conn);
                ins.Parameters.AddWithValue("@darId", darId);
                ins.Parameters.AddWithValue("@sam", samAcc);
                ins.Parameters.AddWithValue("@name", fullName);
                ins.Parameters.AddWithValue("@dep", depCode);
                ins.Parameters.AddWithValue("@depart", depart);
                ins.Parameters.AddWithValue("@rt", roleType);
                ins.Parameters.AddWithValue("@ord", order++);
                ins.ExecuteNonQuery();
            }
        }
        private string GenerateDarNo()
        {
            var year = DateTime.Now.Year;
            try
            {
                using var conn = _db.GetQCDarConnection();
                conn.Open();
                const string sql = @"
                    SELECT ISNULL(MAX(CAST(RIGHT(DarNo,5) AS INT)), 0) + 1
                    FROM   [dbo].[dar_Master]
                    WHERE  DarNo LIKE @prefix";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@prefix", $"DAR-{year}-%");
                var seq = (int)cmd.ExecuteScalar();
                return $"DAR-{year}-{seq:D5}";
            }
            catch { return $"DAR-{year}-00001"; }
        }

        private int InsertDar(DarMasterModel m)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = @"
                INSERT INTO [dbo].[dar_Master]
                    (DarNo, DocType, DocTypeOther, ForStandard, ForStandardOther,
                     DocumentNo, DocumentName, Purpose, PurposeOther,
                     Content, HasAttachment, AttachmentFileName, DocStatusUnderRequest,
                     ReasonBehindPurpose, EffectiveDate, RevisionNo,
                     IsControlledCopy, IsUncontrolledCopy, DistributionList,
                     ReviewerSamAcc, ReviewerName, ReviewerEmail,
                     ApproverSamAcc, ApproverName, ApproverEmail,
                     RequestedBySamAcc, RequestedByName, RequestedDate,
                     Status, Remarks, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.DarId
                VALUES
                    (@DarNo, @DocType, @DocTypeOther, @ForStandard, @ForStandardOther,
                     @DocumentNo, @DocumentName, @Purpose, @PurposeOther,
                     @Content, @HasAttachment, @AttachmentFileName, @DocStatusUnderRequest,
                     @ReasonBehindPurpose, @EffectiveDate, @RevisionNo,
                     @IsControlledCopy, @IsUncontrolledCopy, @DistributionList,
                     @ReviewerSamAcc, @ReviewerName, @ReviewerEmail,
                     @ApproverSamAcc, @ApproverName, @ApproverEmail,
                     @RequestedBySamAcc, @RequestedByName, @RequestedDate,
                     @Status, @Remarks, GETDATE(), GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            BindDarParams(cmd, m);
            return (int)cmd.ExecuteScalar();
        }

        private void UpdateDar(DarMasterModel m)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = @"
                UPDATE [dbo].[dar_Master] SET
                    DocType=@DocType, DocTypeOther=@DocTypeOther,
                    ForStandard=@ForStandard, ForStandardOther=@ForStandardOther,
                    DocumentNo=@DocumentNo, DocumentName=@DocumentName,
                    Purpose=@Purpose, PurposeOther=@PurposeOther,
                    Content=@Content, HasAttachment=@HasAttachment,
                    DocStatusUnderRequest=@DocStatusUnderRequest,
                    ReasonBehindPurpose=@ReasonBehindPurpose,
                    EffectiveDate=@EffectiveDate, RevisionNo=@RevisionNo,
                    IsControlledCopy=@IsControlledCopy, IsUncontrolledCopy=@IsUncontrolledCopy,
                    DistributionList=@DistributionList, UpdatedAt=GETDATE()
                WHERE DarId=@DarId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DarId", m.DarId);
            BindDarParams(cmd, m);
            cmd.ExecuteNonQuery();
        }

        private void UpdateStatus(int darId, DarStatus status,
                                  string? approvedBySam = null,
                                  string? approvedByName = null,
                                  DateTime? approvedDate = null,
                                  string? remarks = null)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = @"
                UPDATE [dbo].[dar_Master] SET
                    Status=@Status,
                    ApprovedBySamAcc=COALESCE(@ApprovedBySam, ApprovedBySamAcc),
                    ApprovedByName=COALESCE(@ApprovedByName, ApprovedByName),
                    ApprovedDate=COALESCE(@ApprovedDate, ApprovedDate),
                    Remarks=COALESCE(@Remarks, Remarks),
                    UpdatedAt=GETDATE()
                WHERE DarId=@DarId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DarId", darId);
            cmd.Parameters.AddWithValue("@Status", (int)status);
            cmd.Parameters.AddWithValue("@ApprovedBySam", (object?)approvedBySam ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ApprovedByName", (object?)approvedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ApprovedDate", (object?)approvedDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private void UpdateMR(int darId, bool agree, string mrSam, DateTime mrDate,
                               DarStatus nextStatus, string? remarks)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = @"
                UPDATE [dbo].[dar_Master] SET
                    MRAgree=@MRAgree, MRSamAcc=@MRSamAcc, MRDate=@MRDate,
                    Status=@Status, Remarks=COALESCE(@Remarks, Remarks),
                    UpdatedAt=GETDATE()
                WHERE DarId=@DarId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DarId", darId);
            cmd.Parameters.AddWithValue("@MRAgree", agree);
            cmd.Parameters.AddWithValue("@MRSamAcc", mrSam);
            cmd.Parameters.AddWithValue("@MRDate", mrDate);
            cmd.Parameters.AddWithValue("@Status", (int)nextStatus);
            cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private void UpdateDCO(int darId, string dcoSam, DateTime regDate,
                               DarStatus nextStatus, string? remarks)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = @"
                UPDATE [dbo].[dar_Master] SET
                    DCOSamAcc=@DCOSamAcc, DocRegisteredDate=@RegDate,
                    Status=@Status, Remarks=COALESCE(@Remarks, Remarks),
                    UpdatedAt=GETDATE()
                WHERE DarId=@DarId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DarId", darId);
            cmd.Parameters.AddWithValue("@DCOSamAcc", dcoSam);
            cmd.Parameters.AddWithValue("@RegDate", regDate);
            cmd.Parameters.AddWithValue("@Status", (int)nextStatus);
            cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private DarMasterModel? GetDarById(int id)
        {
            using var conn = _db.GetQCDarConnection();
            conn.Open();
            const string sql = "SELECT * FROM [dbo].[dar_Master] WHERE DarId=@id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;
            return MapDar(rdr);
        }

        private List<DarListItemModel> GetDarList(UserSessionModel session)
        {
            var list = new List<DarListItemModel>();
            using var conn = _db.GetQCDarConnection();
            conn.Open();

            // Admins see all; requesters see their own
            var sql = session.IsAdmin
                ? "SELECT DarId,DarNo,DocumentNo,DocumentName,Purpose,RequestedByName,RequestedDate,Status FROM [dbo].[dar_Master] ORDER BY CreatedAt DESC"
                : "SELECT DarId,DarNo,DocumentNo,DocumentName,Purpose,RequestedByName,RequestedDate,Status FROM [dbo].[dar_Master] WHERE RequestedBySamAcc=@sam ORDER BY CreatedAt DESC";

            using var cmd = new SqlCommand(sql, conn);
            if (!session.IsAdmin)
                cmd.Parameters.AddWithValue("@sam", session.SamAcc);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new DarListItemModel
                {
                    DarId = (int)rdr["DarId"],
                    DarNo = rdr["DarNo"].ToString()!,
                    DocumentNo = rdr["DocumentNo"].ToString()!,
                    DocumentName = rdr["DocumentName"].ToString()!,
                    Purpose = ((DarPurpose)(int)rdr["Purpose"]).ToString(),
                    RequestedBy = rdr["RequestedByName"].ToString()!,
                    RequestedDate = (DateTime)rdr["RequestedDate"],
                    Status = (DarStatus)(int)rdr["Status"],
                });
            }
            return list;
        }

        private List<DarListItemModel> GetPendingList(UserSessionModel session)
        {
            var list = new List<DarListItemModel>();
            using var conn = _db.GetQCDarConnection();
            conn.Open();

            // Filter pending items based on user's role
            var statusFilter = session.IsMR ? (int)DarStatus.PendingMR
                             : session.IsDCO ? (int)DarStatus.PendingDCO
                             : (int)DarStatus.PendingApproval;

            if (session.IsAdmin) statusFilter = -1; // all pending

            var sql = session.IsAdmin
                ? @"SELECT DarId,DarNo,DocumentNo,DocumentName,Purpose,RequestedByName,RequestedDate,Status
                    FROM [dbo].[dar_Master]
                    WHERE Status IN (1,2,3) ORDER BY CreatedAt ASC"
                : @"SELECT DarId,DarNo,DocumentNo,DocumentName,Purpose,RequestedByName,RequestedDate,Status
                    FROM [dbo].[dar_Master]
                    WHERE Status=@status ORDER BY CreatedAt ASC";

            using var cmd = new SqlCommand(sql, conn);
            if (!session.IsAdmin)
                cmd.Parameters.AddWithValue("@status", statusFilter);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new DarListItemModel
                {
                    DarId = (int)rdr["DarId"],
                    DarNo = rdr["DarNo"].ToString()!,
                    DocumentNo = rdr["DocumentNo"].ToString()!,
                    DocumentName = rdr["DocumentName"].ToString()!,
                    Purpose = ((DarPurpose)(int)rdr["Purpose"]).ToString(),
                    RequestedBy = rdr["RequestedByName"].ToString()!,
                    RequestedDate = (DateTime)rdr["RequestedDate"],
                    Status = (DarStatus)(int)rdr["Status"],
                });
            }
            return list;
        }


        private static UserDropdownModel MapUserDropdown(SqlDataReader r)
        {
            return new UserDropdownModel
            {
                SamAcc = r["SamAcc"].ToString() ?? string.Empty,
                FullName = r["FullName"].ToString() ?? string.Empty,
                Email = r["Email"].ToString() ?? string.Empty,
                DepCode = r["DepCode"].ToString() ?? string.Empty,
                Department = r["Department"].ToString() ?? string.Empty,
                RoleType = r["RoleType"] != DBNull.Value ? (int)r["RoleType"] : 0,
                RoleName = r["RoleName"].ToString() ?? string.Empty,
            };
        }
        private static DarMasterModel MapDar(SqlDataReader r)
        {
            return new DarMasterModel
            {
                DarId = (int)r["DarId"],
                DarNo = r["DarNo"].ToString()!,
                DocType = (DarDocType)(int)r["DocType"],
                DocTypeOther = r["DocTypeOther"].ToString()!,
                ForStandard = (DarForStandard)(int)r["ForStandard"],
                ForStandardOther = r["ForStandardOther"].ToString()!,
                DocumentNo = r["DocumentNo"].ToString()!,
                DocumentName = r["DocumentName"].ToString()!,
                Purpose = (DarPurpose)(int)r["Purpose"],
                PurposeOther = r["PurposeOther"].ToString()!,
                Content = r["Content"].ToString()!,
                HasAttachment = (bool)r["HasAttachment"],
                DocStatusUnderRequest = r["DocStatusUnderRequest"].ToString()!,
                ReasonBehindPurpose = r["ReasonBehindPurpose"].ToString()!,
                EffectiveDate = r["EffectiveDate"] as DateTime?,
                RevisionNo = r["RevisionNo"].ToString()!,
                IsControlledCopy = (bool)r["IsControlledCopy"],
                IsUncontrolledCopy = (bool)r["IsUncontrolledCopy"],
                DistributionList = r["DistributionList"].ToString()!,
                RequestedBySamAcc = r["RequestedBySamAcc"].ToString()!,
                RequestedByName = r["RequestedByName"].ToString()!,
                RequestedDate = (DateTime)r["RequestedDate"],
                ApprovedBySamAcc = r["ApprovedBySamAcc"] as string,
                ApprovedByName = r["ApprovedByName"] as string,
                ApprovedDate = r["ApprovedDate"] as DateTime?,
                MRAgree = r["MRAgree"] as bool?,
                MRSamAcc = r["MRSamAcc"] as string,
                MRDate = r["MRDate"] as DateTime?,
                DCOSamAcc = r["DCOSamAcc"] as string,
                DocRegisteredDate = r["DocRegisteredDate"] as DateTime?,
                Status = (DarStatus)(int)r["Status"],
                Remarks = r["Remarks"].ToString()!,
                CreatedAt = (DateTime)r["CreatedAt"],
                AttachmentFileName = r["AttachmentFileName"].ToString()!,
                ReviewerSamAcc = r["ReviewerSamAcc"].ToString()!,
                ReviewerName = r["ReviewerName"].ToString()!,
                ReviewerEmail = r["ReviewerEmail"].ToString()!,
                ReviewedDate = r["ReviewedDate"] as DateTime?,
                ApproverSamAcc = r["ApproverSamAcc"].ToString()!,
                ApproverName = r["ApproverName"].ToString()!,
                ApproverEmail = r["ApproverEmail"].ToString()!,
                UpdatedAt = (DateTime)r["UpdatedAt"],
            };
        }

        private static void BindDarParams(SqlCommand cmd, DarMasterModel m)
        {
            cmd.Parameters.AddWithValue("@DarNo", m.DarNo);
            cmd.Parameters.AddWithValue("@DocType", (int)m.DocType);
            cmd.Parameters.AddWithValue("@DocTypeOther", m.DocTypeOther);
            cmd.Parameters.AddWithValue("@ForStandard", (int)m.ForStandard);
            cmd.Parameters.AddWithValue("@ForStandardOther", m.ForStandardOther);
            cmd.Parameters.AddWithValue("@DocumentNo", m.DocumentNo);
            cmd.Parameters.AddWithValue("@DocumentName", m.DocumentName);
            cmd.Parameters.AddWithValue("@Purpose", (int)m.Purpose);
            cmd.Parameters.AddWithValue("@PurposeOther", m.PurposeOther);
            cmd.Parameters.AddWithValue("@Content", m.Content);
            cmd.Parameters.AddWithValue("@HasAttachment", m.HasAttachment);
            cmd.Parameters.AddWithValue("@AttachmentFileName", m.AttachmentFileName);
            cmd.Parameters.AddWithValue("@DocStatusUnderRequest", m.DocStatusUnderRequest);
            cmd.Parameters.AddWithValue("@ReviewerSamAcc", m.ReviewerSamAcc);
            cmd.Parameters.AddWithValue("@ReviewerName", m.ReviewerName);
            cmd.Parameters.AddWithValue("@ReviewerEmail", m.ReviewerEmail);
            cmd.Parameters.AddWithValue("@ApproverSamAcc", m.ApproverSamAcc);
            cmd.Parameters.AddWithValue("@ApproverName", m.ApproverName);
            cmd.Parameters.AddWithValue("@ApproverEmail", m.ApproverEmail);
            cmd.Parameters.AddWithValue("@ReasonBehindPurpose", m.ReasonBehindPurpose);
            cmd.Parameters.AddWithValue("@EffectiveDate", (object?)m.EffectiveDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevisionNo", m.RevisionNo);
            cmd.Parameters.AddWithValue("@IsControlledCopy", m.IsControlledCopy);
            cmd.Parameters.AddWithValue("@IsUncontrolledCopy", m.IsUncontrolledCopy);
            cmd.Parameters.AddWithValue("@DistributionList", m.DistributionList);
            cmd.Parameters.AddWithValue("@RequestedBySamAcc", m.RequestedBySamAcc);
            cmd.Parameters.AddWithValue("@RequestedByName", m.RequestedByName);
            cmd.Parameters.AddWithValue("@RequestedDate", m.RequestedDate);
            cmd.Parameters.AddWithValue("@Status", (int)m.Status);
            cmd.Parameters.AddWithValue("@Remarks", m.Remarks);
        }
    }
}
