-- ============================================================
-- Migration: Digital Signature columns + SP
-- Database : BT_QCDAR
-- Version  : 3.0.0
-- Based on  : DigitalSign API (POST /api/sign)
-- ============================================================

USE BT_QCDAR;
GO

-- ── Add signature columns to dar_Master ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerSignedAt')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerSignedAt]       DATETIME      NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerSignatureBase64')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerSignatureBase64] NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerCertThumbprint')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerCertThumbprint]  NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverSignedAt')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverSignedAt]        DATETIME      NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverSignatureBase64')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverSignatureBase64] NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverCertThumbprint')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverCertThumbprint]  NVARCHAR(200) NULL;

PRINT 'dar_Master signature columns added.';
GO

-- ── usp_SaveSignature ─────────────────────────────────────────
-- Called after POST /api/sign returns success
-- Role: 'Reviewer' or 'Approver'
IF OBJECT_ID('dbo.usp_SaveSignature', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SaveSignature;
GO

CREATE PROCEDURE dbo.usp_SaveSignature
    @DarId              INT,
    @Role               NVARCHAR(20),    -- 'Reviewer' or 'Approver'
    @SignedAt           DATETIME,
    @SignatureBase64    NVARCHAR(MAX),
    @CertThumbprint     NVARCHAR(200),
    @NextStatus         INT              -- DarStatus to advance to
AS
BEGIN
    SET NOCOUNT ON;

    IF @Role = 'Reviewer'
    BEGIN
        UPDATE [dbo].[dar_Master]
        SET
            ReviewerSignedAt       = @SignedAt,
            ReviewerSignatureBase64 = @SignatureBase64,
            ReviewerCertThumbprint  = @CertThumbprint,
            Status                 = @NextStatus,
            UpdatedAt              = GETDATE()
        WHERE DarId = @DarId;
    END
    ELSE IF @Role = 'Approver'
    BEGIN
        UPDATE [dbo].[dar_Master]
        SET
            ApproverSignedAt       = @SignedAt,
            ApproverSignatureBase64 = @SignatureBase64,
            ApproverCertThumbprint  = @CertThumbprint,
            Status                 = @NextStatus,
            UpdatedAt              = GETDATE()
        WHERE DarId = @DarId;
    END

    SELECT @@ROWCOUNT AS AffectedRows;
END
GO

PRINT 'SP usp_SaveSignature created.';
GO
PRINT '=== Migration 3.0.0 Complete ===';
GO
