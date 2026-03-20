using BTQCDar.Models;
using BTQCDar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BTQCDar.Controllers
{
    public class DarController : BaseController
    {
        private readonly IDbService _db;

        public DarController(IDbService db)
        {
            _db = db;
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
                RequestedByName   = session.FullName,
                RequestedDate     = DateTime.Now,
            };

            ViewBag.Session = session;
            return View(model);
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /Dar/Create
        // ────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DarMasterModel model)
        {
            var redirect = RequireLogin(out var session);
            if (redirect != null) return redirect;

            if (!ModelState.IsValid)
            {
                ViewBag.Session = session;
                return View(model);
            }

            model.RequestedBySamAcc = session.SamAcc;
            model.RequestedByName   = session.FullName;
            model.RequestedDate     = DateTime.Now;
            model.Status            = DarStatus.PendingApproval;
            model.DarNo             = GenerateDarNo();

            int newId = InsertDar(model);

            TempData["Success"] = $"สร้าง DAR เรียบร้อย : {model.DarNo}";
            return RedirectToAction("Detail", new { id = newId });
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
                TempData["Error"] = "ไม่สามารถแก้ไขได้ เอกสารอยู่ระหว่างดำเนินการแล้ว";
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
            TempData["Success"] = "บันทึกการแก้ไขเรียบร้อย";
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

            TempData["Success"] = "Approve เรียบร้อย — ส่งต่อ MR แล้ว";
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

            TempData["Success"] = agree ? "MR Agree — ส่งต่อ DCO" : "MR ไม่อนุมัติ";
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

            TempData["Success"] = "DCO ลงทะเบียนเอกสารเรียบร้อย — DAR สมบูรณ์";
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
            TempData["Error"] = "Reject เอกสารแล้ว";
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
                        draft     = rdr["draft"],
                        pending   = rdr["pending"],
                        completed = rdr["completed"],
                        rejected  = rdr["rejected"]
                    });
            }
            catch { /* Return zeros on error */ }

            return Json(new { draft = 0, pending = 0, completed = 0, rejected = 0 });
        }

        // ══════════════════════════════════════════════════════════════════
        // DATA ACCESS  (raw ADO.NET — no EF, consistent with BTTemplate)
        // ══════════════════════════════════════════════════════════════════

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
                     Content, HasAttachment, DocStatusUnderRequest,
                     ReasonBehindPurpose, EffectiveDate, RevisionNo,
                     IsControlledCopy, IsUncontrolledCopy, DistributionList,
                     RequestedBySamAcc, RequestedByName, RequestedDate,
                     Status, Remarks, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.DarId
                VALUES
                    (@DarNo, @DocType, @DocTypeOther, @ForStandard, @ForStandardOther,
                     @DocumentNo, @DocumentName, @Purpose, @PurposeOther,
                     @Content, @HasAttachment, @DocStatusUnderRequest,
                     @ReasonBehindPurpose, @EffectiveDate, @RevisionNo,
                     @IsControlledCopy, @IsUncontrolledCopy, @DistributionList,
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
            cmd.Parameters.AddWithValue("@DarId",        darId);
            cmd.Parameters.AddWithValue("@Status",       (int)status);
            cmd.Parameters.AddWithValue("@ApprovedBySam",  (object?)approvedBySam  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ApprovedByName", (object?)approvedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ApprovedDate",   (object?)approvedDate   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks",        (object?)remarks        ?? DBNull.Value);
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
            cmd.Parameters.AddWithValue("@DarId",    darId);
            cmd.Parameters.AddWithValue("@MRAgree",  agree);
            cmd.Parameters.AddWithValue("@MRSamAcc", mrSam);
            cmd.Parameters.AddWithValue("@MRDate",   mrDate);
            cmd.Parameters.AddWithValue("@Status",   (int)nextStatus);
            cmd.Parameters.AddWithValue("@Remarks",  (object?)remarks ?? DBNull.Value);
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
            cmd.Parameters.AddWithValue("@DarId",    darId);
            cmd.Parameters.AddWithValue("@DCOSamAcc", dcoSam);
            cmd.Parameters.AddWithValue("@RegDate",  regDate);
            cmd.Parameters.AddWithValue("@Status",   (int)nextStatus);
            cmd.Parameters.AddWithValue("@Remarks",  (object?)remarks ?? DBNull.Value);
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
                    DarId         = (int)rdr["DarId"],
                    DarNo         = rdr["DarNo"].ToString()!,
                    DocumentNo    = rdr["DocumentNo"].ToString()!,
                    DocumentName  = rdr["DocumentName"].ToString()!,
                    Purpose       = ((DarPurpose)(int)rdr["Purpose"]).ToString(),
                    RequestedBy   = rdr["RequestedByName"].ToString()!,
                    RequestedDate = (DateTime)rdr["RequestedDate"],
                    Status        = (DarStatus)(int)rdr["Status"],
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
            var statusFilter = session.IsMR  ? (int)DarStatus.PendingMR
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
                    DarId         = (int)rdr["DarId"],
                    DarNo         = rdr["DarNo"].ToString()!,
                    DocumentNo    = rdr["DocumentNo"].ToString()!,
                    DocumentName  = rdr["DocumentName"].ToString()!,
                    Purpose       = ((DarPurpose)(int)rdr["Purpose"]).ToString(),
                    RequestedBy   = rdr["RequestedByName"].ToString()!,
                    RequestedDate = (DateTime)rdr["RequestedDate"],
                    Status        = (DarStatus)(int)rdr["Status"],
                });
            }
            return list;
        }

        private static DarMasterModel MapDar(SqlDataReader r)
        {
            return new DarMasterModel
            {
                DarId                 = (int)r["DarId"],
                DarNo                 = r["DarNo"].ToString()!,
                DocType               = (DarDocType)(int)r["DocType"],
                DocTypeOther          = r["DocTypeOther"].ToString()!,
                ForStandard           = (DarForStandard)(int)r["ForStandard"],
                ForStandardOther      = r["ForStandardOther"].ToString()!,
                DocumentNo            = r["DocumentNo"].ToString()!,
                DocumentName          = r["DocumentName"].ToString()!,
                Purpose               = (DarPurpose)(int)r["Purpose"],
                PurposeOther          = r["PurposeOther"].ToString()!,
                Content               = r["Content"].ToString()!,
                HasAttachment         = (bool)r["HasAttachment"],
                DocStatusUnderRequest = r["DocStatusUnderRequest"].ToString()!,
                ReasonBehindPurpose   = r["ReasonBehindPurpose"].ToString()!,
                EffectiveDate         = r["EffectiveDate"] as DateTime?,
                RevisionNo            = r["RevisionNo"].ToString()!,
                IsControlledCopy      = (bool)r["IsControlledCopy"],
                IsUncontrolledCopy    = (bool)r["IsUncontrolledCopy"],
                DistributionList      = r["DistributionList"].ToString()!,
                RequestedBySamAcc     = r["RequestedBySamAcc"].ToString()!,
                RequestedByName       = r["RequestedByName"].ToString()!,
                RequestedDate         = (DateTime)r["RequestedDate"],
                ApprovedBySamAcc      = r["ApprovedBySamAcc"] as string,
                ApprovedByName        = r["ApprovedByName"] as string,
                ApprovedDate          = r["ApprovedDate"] as DateTime?,
                MRAgree               = r["MRAgree"] as bool?,
                MRSamAcc              = r["MRSamAcc"] as string,
                MRDate                = r["MRDate"] as DateTime?,
                DCOSamAcc             = r["DCOSamAcc"] as string,
                DocRegisteredDate     = r["DocRegisteredDate"] as DateTime?,
                Status                = (DarStatus)(int)r["Status"],
                Remarks               = r["Remarks"].ToString()!,
                CreatedAt             = (DateTime)r["CreatedAt"],
                UpdatedAt             = (DateTime)r["UpdatedAt"],
            };
        }

        private static void BindDarParams(SqlCommand cmd, DarMasterModel m)
        {
            cmd.Parameters.AddWithValue("@DarNo",                 m.DarNo);
            cmd.Parameters.AddWithValue("@DocType",               (int)m.DocType);
            cmd.Parameters.AddWithValue("@DocTypeOther",          m.DocTypeOther);
            cmd.Parameters.AddWithValue("@ForStandard",           (int)m.ForStandard);
            cmd.Parameters.AddWithValue("@ForStandardOther",      m.ForStandardOther);
            cmd.Parameters.AddWithValue("@DocumentNo",            m.DocumentNo);
            cmd.Parameters.AddWithValue("@DocumentName",          m.DocumentName);
            cmd.Parameters.AddWithValue("@Purpose",               (int)m.Purpose);
            cmd.Parameters.AddWithValue("@PurposeOther",          m.PurposeOther);
            cmd.Parameters.AddWithValue("@Content",               m.Content);
            cmd.Parameters.AddWithValue("@HasAttachment",         m.HasAttachment);
            cmd.Parameters.AddWithValue("@DocStatusUnderRequest", m.DocStatusUnderRequest);
            cmd.Parameters.AddWithValue("@ReasonBehindPurpose",   m.ReasonBehindPurpose);
            cmd.Parameters.AddWithValue("@EffectiveDate",   (object?)m.EffectiveDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevisionNo",            m.RevisionNo);
            cmd.Parameters.AddWithValue("@IsControlledCopy",      m.IsControlledCopy);
            cmd.Parameters.AddWithValue("@IsUncontrolledCopy",    m.IsUncontrolledCopy);
            cmd.Parameters.AddWithValue("@DistributionList",      m.DistributionList);
            cmd.Parameters.AddWithValue("@RequestedBySamAcc",     m.RequestedBySamAcc);
            cmd.Parameters.AddWithValue("@RequestedByName",       m.RequestedByName);
            cmd.Parameters.AddWithValue("@RequestedDate",         m.RequestedDate);
            cmd.Parameters.AddWithValue("@Status",                (int)m.Status);
            cmd.Parameters.AddWithValue("@Remarks",               m.Remarks);
        }
    }
}
