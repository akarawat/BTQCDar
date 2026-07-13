-- ============================================================
-- Migration: QMRPermiss — approval permission flag for QMR role
-- Version  : 3.5.0
-- ============================================================
USE BT_QCDAR;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dar_UserApprovalRoles') AND name = 'QMRPermiss'
)
    ALTER TABLE [dbo].[dar_UserApprovalRoles]
        ADD [QMRPermiss] BIT NOT NULL DEFAULT 0;
GO

PRINT 'QMRPermiss column added to dar_UserApprovalRoles.';
GO

IF OBJECT_ID('dbo.usp_GetUserRoles', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetUserRoles;
GO

CREATE PROCEDURE dbo.usp_GetUserRoles
    @SamAcc NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsApprover BIT = 0, @IsAdmin BIT = 0;
    SELECT @IsApprover = ISNULL(IsApprover, 0), @IsAdmin = ISNULL(IsAdmin, 0)
    FROM [dbo].[dar_UserRoles]
    WHERE LOWER(SamAcc) = LOWER(@SamAcc);

    DECLARE @IsMR BIT = 0, @IsDCO BIT = 0, @QMRPermiss BIT = 0;

    IF EXISTS (
        SELECT 1 FROM [dbo].[dar_UserApprovalRoles]
        WHERE LOWER(SamAcc) = LOWER(@SamAcc) AND RoleType = 2 AND IsActive = 1
    )
    BEGIN
        SET @IsMR = 1;
        SELECT @QMRPermiss = ISNULL(MAX(CAST(QMRPermiss AS INT)), 0)
        FROM [dbo].[dar_UserApprovalRoles]
        WHERE LOWER(SamAcc) = LOWER(@SamAcc) AND RoleType = 2 AND IsActive = 1;
    END

    IF EXISTS (
        SELECT 1 FROM [dbo].[dar_UserApprovalRoles]
        WHERE LOWER(SamAcc) = LOWER(@SamAcc) AND RoleType = 1 AND IsActive = 1
    )   SET @IsDCO = 1;

    -- Backward compat with legacy dar_UserRoles
    SELECT
        @IsMR  = CASE WHEN @IsMR  = 1 THEN 1 ELSE ISNULL(IsMR,  0) END,
        @IsDCO = CASE WHEN @IsDCO = 1 THEN 1 ELSE ISNULL(IsDCO, 0) END
    FROM [dbo].[dar_UserRoles]
    WHERE LOWER(SamAcc) = LOWER(@SamAcc);

    SELECT
        @IsApprover AS IsApprover,
        @IsMR       AS IsMR,
        @IsDCO      AS IsDCO,
        @IsAdmin    AS IsAdmin,
        @QMRPermiss AS QMRPermiss;
END
GO

PRINT 'usp_GetUserRoles v1.2 — QMRPermiss added.';
GO
PRINT '=== Migration 3.5.0 Complete ===';
GO
