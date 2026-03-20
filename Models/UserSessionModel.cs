namespace BTQCDar.Models
{
    /// <summary>
    /// User session stored as JSON in ASP.NET Session.
    /// Populated after SSO callback and role-load from DB.
    /// </summary>
    public class UserSessionModel
    {
        // ── SSO fields (from callback query string) ───────────────────
        public string UserId    { get; set; } = string.Empty;   // ?id=
        public string SamAcc    { get; set; } = string.Empty;   // ?user=  (Windows login)
        public string Email     { get; set; } = string.Empty;   // ?email=
        public string FullName  { get; set; } = string.Empty;   // ?fname=
        public string Dept      { get; set; } = string.Empty;   // ?depart=

        // ── HR fields (from BT_HR.onl_TBADUsers) ─────────────────────
        public string ManagerSamAcc  { get; set; } = string.Empty;
        public string ManagerName    { get; set; } = string.Empty;
        public string ManagerEmail   { get; set; } = string.Empty;

        // ── DAR Role flags (from BT_QCDAR.dar_UserRoles) ─────────────
        public bool IsDarRequester  { get; set; } = true;   // everyone can request
        public bool IsDarApprover   { get; set; } = false;
        public bool IsMR            { get; set; } = false;  // Management Representative
        public bool IsDCO           { get; set; } = false;  // Document Control Officer
        public bool IsAdmin         { get; set; } = false;
    }
}
