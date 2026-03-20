namespace BTQCDar.Models
{
    // ─── Document Type (Type section) ────────────────────────────────────────
    public enum DarDocType
    {
        PolicyObjectiveManual   = 1,
        StandardProcedure       = 2,
        WorkInstruction         = 3,
        FormAndFormat           = 4,
        SupportingDocument      = 5,
        Drawing                 = 6,
        ExternalOrigin          = 7,
        Other                   = 8
    }

    // ─── For (Standards) ────────────────────────────────────────────────────
    public enum DarForStandard
    {
        QMS_ISO9001     = 1,
        EMS_ISO14001    = 2,
        Safety_OHSAS    = 3,
        Others          = 4
    }

    // ─── Purpose ─────────────────────────────────────────────────────────────
    public enum DarPurpose
    {
        IssueNew            = 1,
        ReceiveExternal     = 2,
        ChangeUpdateRevise  = 3,
        AddDistribution     = 4,
        RequestCopy         = 5,
        CancelDocument      = 6,
        Others              = 7
    }

    // ─── Workflow Status ─────────────────────────────────────────────────────
    public enum DarStatus
    {
        Draft           = 0,
        PendingApproval = 1,
        PendingMR       = 2,
        PendingDCO      = 3,
        Completed       = 4,
        Rejected        = 5,
        Cancelled       = 6
    }

    // ─── Master DAR record ───────────────────────────────────────────────────
    public class DarMasterModel
    {
        // PK / DAR Number
        public int    DarId      { get; set; }
        public string DarNo      { get; set; } = string.Empty;   // auto: DAR-YYYY-XXXXX

        // Type section
        public DarDocType DocType        { get; set; }
        public string     DocTypeOther   { get; set; } = string.Empty;

        // For section
        public DarForStandard ForStandard       { get; set; }
        public string          ForStandardOther  { get; set; } = string.Empty;

        // Document info
        public string DocumentNo   { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;

        // Purpose section
        public DarPurpose Purpose       { get; set; }
        public string      PurposeOther  { get; set; } = string.Empty;

        // Content
        public string Content       { get; set; } = string.Empty;
        public bool   HasAttachment { get; set; } = false;

        // Status of document under request
        public string DocStatusUnderRequest { get; set; } = string.Empty;

        // Reason
        public string ReasonBehindPurpose { get; set; } = string.Empty;

        // Effective date & revision
        public DateTime? EffectiveDate { get; set; }
        public string    RevisionNo    { get; set; } = string.Empty;

        // Distribution list type
        public bool IsControlledCopy   { get; set; } = true;
        public bool IsUncontrolledCopy { get; set; } = false;
        public string DistributionList { get; set; } = string.Empty;

        // Requestor
        public string RequestedBySamAcc { get; set; } = string.Empty;
        public string RequestedByName   { get; set; } = string.Empty;
        public DateTime RequestedDate   { get; set; } = DateTime.Now;

        // Approver
        public string?   ApprovedBySamAcc { get; set; }
        public string?   ApprovedByName   { get; set; }
        public DateTime? ApprovedDate     { get; set; }

        // MR section
        public bool?     MRAgree   { get; set; }
        public string?   MRSamAcc  { get; set; }
        public DateTime? MRDate    { get; set; }

        // DCO section
        public string?   DCOSamAcc         { get; set; }
        public DateTime? DocRegisteredDate { get; set; }

        // Workflow status
        public DarStatus Status   { get; set; } = DarStatus.Draft;
        public string    Remarks  { get; set; } = string.Empty;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        public List<DarDistributionModel> DistributionRecords { get; set; } = new();
        public List<DarRelatedDocModel>   RelatedDocuments    { get; set; } = new();
    }

    // ─── Distribution Record (Controlled Copy tracking) ─────────────────────
    public class DarDistributionModel
    {
        public int    DistId       { get; set; }
        public int    DarId        { get; set; }
        public string DeptSect     { get; set; } = string.Empty;
        public string ReceiveSign  { get; set; } = string.Empty;
        public DateTime? ReceiveDate { get; set; }
        public string ReturnSign   { get; set; } = string.Empty;
        public DateTime? ReturnDate  { get; set; }
        public int    SortOrder    { get; set; }
    }

    // ─── Related Documents ───────────────────────────────────────────────────
    public class DarRelatedDocModel
    {
        public int    RelDocId     { get; set; }
        public int    DarId        { get; set; }
        public int    ItemNo       { get; set; }
        public string DocumentNo   { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public bool   IsRevise     { get; set; } = false;
        public bool   IsKeepAsIs  { get; set; } = false;
    }

    // ─── List/Index view model ───────────────────────────────────────────────
    public class DarListItemModel
    {
        public int      DarId          { get; set; }
        public string   DarNo          { get; set; } = string.Empty;
        public string   DocumentNo     { get; set; } = string.Empty;
        public string   DocumentName   { get; set; } = string.Empty;
        public string   Purpose        { get; set; } = string.Empty;
        public string   RequestedBy    { get; set; } = string.Empty;
        public DateTime RequestedDate  { get; set; }
        public DarStatus Status        { get; set; }
        public string   StatusLabel    => Status switch
        {
            DarStatus.Draft           => "Draft",
            DarStatus.PendingApproval => "Pending Approval",
            DarStatus.PendingMR       => "Pending MR",
            DarStatus.PendingDCO      => "Pending DCO",
            DarStatus.Completed       => "Completed",
            DarStatus.Rejected        => "Rejected",
            DarStatus.Cancelled       => "Cancelled",
            _                         => "Unknown"
        };
        public string StatusBadgeClass => Status switch
        {
            DarStatus.Draft           => "bg-secondary",
            DarStatus.PendingApproval => "bg-warning text-dark",
            DarStatus.PendingMR       => "bg-info text-dark",
            DarStatus.PendingDCO      => "bg-primary",
            DarStatus.Completed       => "bg-success",
            DarStatus.Rejected        => "bg-danger",
            DarStatus.Cancelled       => "bg-dark",
            _                         => "bg-secondary"
        };
    }
}
