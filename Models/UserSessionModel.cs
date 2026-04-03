namespace BTQCDar.Models
{
    /// <summary>
    /// User session stored as JSON in ASP.NET Session.
    /// Populated after SSO callback and role-load from DB.
    /// </summary>
    public class UserSessionModel
    {
        // SSO fields (from callback query string)
        public string UserId { get; set; } = string.Empty;  // ?id=
        public string SamAcc { get; set; } = string.Empty;  // ?user=
        public string Email { get; set; } = string.Empty;  // ?email=
        public string FullName { get; set; } = string.Empty;  // ?fname=
        //public string Dept { get; set; } = string.Empty;  // ?depart= (display name)

        // HR fields (from usp_GetUserHRInfo → BT_HR.onl_TBADUsers)
        public string DepCode { get; set; } = string.Empty;  // dep_code (numeric dept code)
        public string DepName { get; set; } = string.Empty;  // dep_code (numeric dept code)
        public string ManagerSamAcc { get; set; } = string.Empty;  // FUNC_GetInfoByFullName(reporter,1)
        public string ManagerName { get; set; } = string.Empty;  // reporter (display name)
        public string ManagerEmail { get; set; } = string.Empty;  // FUNC_GetInfoByFullName(reporter,2)

        // DAR Role flags (from BT_QCDAR.dar_UserRoles)
        public bool IsDarRequester { get; set; } = true;   // everyone can request
        public bool IsDarApprover { get; set; } = false;
        public bool IsMR { get; set; } = false;  // Management Representative
        public bool IsDCO { get; set; } = false;  // Document Control Officer
        public bool IsAdmin { get; set; } = false;
    }
}
