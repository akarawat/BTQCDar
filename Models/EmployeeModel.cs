namespace BTQCDar.Models
{
    /// <summary>
    /// Mapped from [BT_HR].[dbo].[onl_TBADUsers]
    /// </summary>
    public class EmployeeModel
    {
        public string SamAcc        { get; set; } = string.Empty;
        public string Email         { get; set; } = string.Empty;
        public string FullName      { get; set; } = string.Empty;
        public string Department    { get; set; } = string.Empty;
        public string ManagerSamAcc { get; set; } = string.Empty;
        public string ManagerName   { get; set; } = string.Empty;
        public string ManagerEmail  { get; set; } = string.Empty;
    }
}
