-- ============================================================
-- Migration: Fix usp_GetUserRoles
-- ปัญหา: IsMR/IsDCO ใน dar_UserRoles ไม่ sync กับ
--        dar_UserApprovalRoles (RoleType 1=DCC, 2=QMR)
-- แก้:   รวม 2 ตาราง ให้ role flag derive จาก
--        dar_UserApprovalRoles โดยอัตโนมัติ
-- Version: 1.1
-- ============================================================

USE BT_QCDAR;
GO

IF OBJECT_ID('dbo.usp_GetUserRoles', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetUserRoles;
GO

CREATE PROCEDURE dbo.usp_GetUserRoles
    @SamAcc NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Flags from dar_UserRoles (IsApprover, IsAdmin)
    DECLARE @IsApprover BIT = 0;
    DECLARE @IsAdmin    BIT = 0;

    SELECT
        @IsApprover = ISNULL(IsApprover, 0),
        @IsAdmin    = ISNULL(IsAdmin,    0)
    FROM [dbo].[dar_UserRoles]
    WHERE LOWER(SamAcc) = LOWER(@SamAcc);

    -- IsMR  = RoleType 2 (QMR) in dar_UserApprovalRoles
    -- IsDCO = RoleType 1 (DCC) in dar_UserApprovalRoles
    DECLARE @IsMR  BIT = 0;
    DECLARE @IsDCO BIT = 0;

    IF EXISTS (
        SELECT 1 FROM [dbo].[dar_UserApprovalRoles]
        WHERE LOWER(SamAcc) = LOWER(@SamAcc)
          AND RoleType = 2     -- QMR
          AND IsActive = 1
    )   SET @IsMR = 1;

    IF EXISTS (
        SELECT 1 FROM [dbo].[dar_UserApprovalRoles]
        WHERE LOWER(SamAcc) = LOWER(@SamAcc)
          AND RoleType = 1     -- DCC
          AND IsActive = 1
    )   SET @IsDCO = 1;

    -- Also check legacy dar_UserRoles IsMR/IsDCO (backward compat)
    SELECT
        @IsMR  = CASE WHEN @IsMR  = 1 THEN 1 ELSE ISNULL(IsMR,  0) END,
        @IsDCO = CASE WHEN @IsDCO = 1 THEN 1 ELSE ISNULL(IsDCO, 0) END
    FROM [dbo].[dar_UserRoles]
    WHERE LOWER(SamAcc) = LOWER(@SamAcc);

    SELECT
        @IsApprover AS IsApprover,
        @IsMR       AS IsMR,
        @IsDCO      AS IsDCO,
        @IsAdmin    AS IsAdmin;
END
GO

PRINT 'usp_GetUserRoles v1.1 updated — IsMR/IsDCO now derived from dar_UserApprovalRoles';
GO
