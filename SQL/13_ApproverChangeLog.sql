-- ============================================================
-- Migration: Admin Reassign Approver — audit log table
-- Version  : 3.3.0
-- ============================================================
USE BT_QCDAR;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'dar_ApproverChangeLog')
BEGIN
    CREATE TABLE [dbo].[dar_ApproverChangeLog] (
        [Id]                 INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DarId]              INT           NOT NULL REFERENCES [dar_Master]([DarId]),
        [DarNo]              NVARCHAR(20)  NOT NULL,
        [ChangedByAdminSam]  NVARCHAR(100) NOT NULL,
        [ChangedByAdminName] NVARCHAR(200) NOT NULL,
        [OldApproverSamAcc]  NVARCHAR(100) NULL,
        [OldApproverName]    NVARCHAR(200) NULL,
        [NewApproverSamAcc]  NVARCHAR(100) NOT NULL,
        [NewApproverName]    NVARCHAR(200) NOT NULL,
        [NewApproverEmail]   NVARCHAR(200) NULL,
        [Reason]             NVARCHAR(MAX) NULL,
        [ChangedAt]          DATETIME      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_dar_ApproverChangeLog_DarId ON [dbo].[dar_ApproverChangeLog]([DarId]);
END
GO

PRINT 'dar_ApproverChangeLog table created.';
GO
PRINT '=== Migration 3.3.0 Complete ===';
GO
