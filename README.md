# BTQCDar — Document Action Request System

**Bernina Thailand** — ASP.NET Core 8 MVC  
ระบบใบขอดำเนินการด้านเอกสาร (Document Action Request : DAR)  
Form Reference: `FM-MR-001, Rev.03, Effective Date: Dec 10th, 2025`

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 MVC (`net8.0`) |
| UI | Bootstrap 5.3 + Bootstrap Icons |
| JavaScript | **jQuery 3.7** (separated `.js` files, no inline scripts) |
| Database (main) | **BT_QCDAR** — SQL Server (new, isolated) |
| Database (HR) | **BT_HR** → `[dbo].[onl_TBADUsers]` (read-only) |
| Auth | BT SSO (`btauthen.berninathailand.com`) |
| Deploy | IIS Self-Contained Publish |
| Data access | Raw ADO.NET (`Microsoft.Data.SqlClient 5.2.2`) |

---

## Project Structure

```
BTQCDar/
├── Controllers/
│   ├── BaseController.cs          ← Session helper (RequireLogin)
│   ├── DashboardsController.cs    ← SSO callback, HR lookup, role load
│   └── DarController.cs           ← Full CRUD + Approve/MR/DCO workflow + Stats API
│
├── Models/
│   ├── AppSettingsModel.cs        ← Bound from appsettings.json
│   ├── UserSessionModel.cs        ← SSO + HR + DAR roles
│   ├── EmployeeModel.cs           ← HR lookup result
│   └── DarModel.cs                ← DarMasterModel, enums, ListItemModel
│
├── Services/
│   ├── IDbService.cs              ← Interface: GetQCDarConnection() / GetHRConnection()
│   └── DbService.cs               ← Implementation (injected via DI)
│
├── Views/
│   ├── Shared/_Layout.cshtml      ← Navbar, Toast, jQuery + Bootstrap CDN
│   ├── _ViewImports.cshtml
│   ├── _ViewStart.cshtml
│   ├── Dashboards/
│   │   └── Index.cshtml           ← Dashboard + stat cards
│   └── Dar/
│       ├── Index.cshtml           ← DAR list (my DARs)
│       ├── Create.cshtml          ← New DAR form (matches PDF layout)
│       ├── Edit.cshtml            ← Edit DAR (Draft only)
│       ├── Detail.cshtml          ← Read-only detail + workflow buttons
│       └── Pending.cshtml         ← Inbox for Approver / MR / DCO
│
├── wwwroot/
│   ├── css/site.css               ← BT brand overrides (Bernina Red)
│   └── js/
│       ├── site.js                ← Global: toast, nav highlight, anti-double-submit
│       ├── dashboard.js           ← Stat counter AJAX loader
│       ├── dar-list.js            ← Live search + column sort
│       ├── dar-form.js            ← Show/hide conditional fields, validation
│       └── dar-detail.js          ← Workflow action modal (Approve/MR/DCO via AJAX)
│
├── SQL/
│   └── 01_CreateDatabase_BT_QCDAR.sql  ← Full DB setup script
│
├── BTQCDar.csproj
├── Program.cs
├── Constants.cs
├── appsettings.json
├── appsettings.Development.json
└── web.config
```

---

## Quick Start (Local)

```bash
# 1. Clone
git clone https://github.com/akarawat/BTQCDar.git
cd BTQCDar

# 2. Setup database (run on your SQL Server)
sqlcmd -S localhost -i SQL/01_CreateDatabase_BT_QCDAR.sql

# 3. Update appsettings.Development.json
#    → ConnectionStrings: BT_QCDAR, BT_HR
#    → TBCorApiServices: URLSITE = https://localhost:5001

# 4. Run
dotnet run
```

---

## SSO Flow

```
GET /Dashboards/Index (no session)
  → Redirect to btauthen.berninathailand.com?url=...
  → SSO authenticates
  → Redirect back with ?id=&user=&email=&fname=&depart=
  → LoadHrInfo()   ← BT_HR: manager name/email
  → LoadUserRoles() ← BT_QCDAR: dar_UserRoles flags
  → Save UserSession (JSON in ASP.NET Session)
  → Show Dashboard
```

---

## DAR Workflow

```
[Requester] Create DAR → Status: Draft
      ↓  Submit
[Approver] Approve/Reject → Status: PendingApproval → PendingMR (or Rejected)
      ↓  Agree
[MR] Agree/Not Agree → Status: PendingMR → PendingDCO (or Rejected)
      ↓  Register
[DCO] Register → Status: PendingDCO → Completed
```

---

## Database

### BT_QCDAR (new — this project)

| Table | Description |
|---|---|
| `dar_Master` | Main DAR record, all form fields, workflow status |
| `dar_Distribution` | Controlled Copy distribution log (Dept, Receive/Return sign) |
| `dar_RelatedDoc` | Related Documents table in PDF form |
| `dar_UserRoles` | Role flags: IsApprover, IsMR, IsDCO, IsAdmin |

### BT_HR (read-only)

| Table | Usage |
|---|---|
| `[dbo].[onl_TBADUsers]` | SamAcc, Email, FullName, Manager (for session load) |

> **Note:** Adjust column names in `DashboardsController.LoadHrInfo()` to match your actual `onl_TBADUsers` schema.

---

## JavaScript Architecture

ทุก `.js` แยกออกจาก `.cshtml` อย่างสมบูรณ์:

| File | Loaded on | Purpose |
|---|---|---|
| `site.js` | ทุกหน้า (via `_Layout`) | Toast, nav highlight, anti-double-submit |
| `dashboard.js` | Dashboard | AJAX load stat counters |
| `dar-list.js` | `/Dar/Index` | Live search + column sort |
| `dar-form.js` | Create / Edit | Toggle conditional fields, client validation |
| `dar-detail.js` | Detail | Bootstrap modal → AJAX POST workflow actions |

**Pattern:** ใช้ `@section Scripts { <script src="~/js/xxx.js"> }` ใน View  
→ ไม่มี inline JavaScript ใน `.cshtml` เลย  
→ แก้ JS ได้โดยไม่ต้อง build ใหม่ (static file)

---

## Roles

| Role | Flag | Can do |
|---|---|---|
| Requester | `IsDarRequester = true` (ทุกคน) | สร้าง / แก้ไข Draft |
| Approver | `IsDarApprover` | Approve / Reject (PendingApproval) |
| MR | `IsMR` | Agree / Not Agree (PendingMR) |
| DCO | `IsDCO` | Register document (PendingDCO) |
| Admin | `IsAdmin` | ทุกอย่าง + เห็น DAR ทั้งหมด |

---

## Publish (IIS)

```bash
dotnet publish -c Release -r win-x64 --self-contained -o ./publish
```

Copy `./publish` → IIS Application folder.

---

## Known Tips (inherited from BTTemplate)

- SQL collation mismatch → ใช้ `COLLATE THAI_CI_AS` ใน JOIN
- `@@keyframes`, `@@media` ใน Razor → ใช้ `@@` แทน `@` (หรือแยกไปไว้ใน `.css`)
- IIS 500.31 → publish Self-Contained หรือ install .NET 8 Hosting Bundle
- SqlClient บน Windows Server เก่า → ใช้ version `5.2.2`
