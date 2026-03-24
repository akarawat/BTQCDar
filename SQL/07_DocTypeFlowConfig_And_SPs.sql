-- ============================================================
-- Migration: DocTypeFlowConfig + SPs for DAR Document Flow
-- Database : BT_QCDAR
-- Version  : 2.0.0
-- ============================================================

USE BT_QCDAR;
GO

-- ────────────────────────────────────────────────────────────
-- 1. dar_DocTypeFlowConfig
--    Stores creator/reviewer/approver role rules per DocType
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'dar_DocTypeFlowConfig')
BEGIN
    CREATE TABLE [dbo].[dar_DocTypeFlowConfig] (
        [DocType]           INT           NOT NULL PRIMARY KEY,
        [DocTypeName]       NVARCHAR(100) NOT NULL,
        [CreatorRoles]      NVARCHAR(50)  NOT NULL,  -- comma-separated e.g. "2,7"  (0=everyone)
        [ReviewerRoles]     NVARCHAR(50)  NOT NULL,  -- comma-separated e.g. "1,2,3,4"
        [ApproverFixedRole] INT           NOT NULL,  -- fixed role: 7=Manager, 8=MD
        [UpdatedAt]         DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Table dar_DocTypeFlowConfig created.';
END
GO

-- Seed / UPSERT flow rules from Excel
MERGE [dbo].[dar_DocTypeFlowConfig] AS target
USING (VALUES
    (1, 'Policy / Objective / Manual',                         '2,7',       '1,2,3,4', 8),
    (2, 'Standard Procedure',                                  '2,5,6,7',   '1,2,3,4', 8),
    (3, 'Work Instruction',                                    '0,2,5,6,7', '1,2,5,6', 7),
    (4, 'Supporting Document',                                 '0,2,5,6,7', '1,2,5,6', 7),
    (5, 'Form and Format',                                     '0,2,5,6,7', '1,2,5,6', 7),
    (6, 'Document from External Origin / Drawing / Other',     '0,1,2,5,6,7','1,2,5,6',7)
) AS src (DocType, DocTypeName, CreatorRoles, ReviewerRoles, ApproverFixedRole)
ON target.DocType = src.DocType
WHEN MATCHED THEN UPDATE SET
    DocTypeName       = src.DocTypeName,
    CreatorRoles      = src.CreatorRoles,
    ReviewerRoles     = src.ReviewerRoles,
    ApproverFixedRole = src.ApproverFixedRole,
    UpdatedAt         = GETDATE()
WHEN NOT MATCHED THEN INSERT (DocType, DocTypeName, CreatorRoles, ReviewerRoles, ApproverFixedRole)
VALUES (src.DocType, src.DocTypeName, src.CreatorRoles, src.ReviewerRoles, src.ApproverFixedRole);
PRINT 'dar_DocTypeFlowConfig seed complete.';
GO

-- ────────────────────────────────────────────────────────────
-- 2. usp_GetAllUserFromAD
--    Returns all active AD users (for Admin role assignment)
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_GetAllUserFromAD', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetAllUserFromAD;
GO
CREATE PROCEDURE dbo.usp_GetAllUserFromAD
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.SamAcc,
        u.Email,
        u.FullName,
        u.DepCode,
        u.Department,
        u.ManagerSamAcc,
        u.ManagerName,
        u.ManagerEmail,
        -- Current role assignment (NULL if not assigned)
        r.RoleType,
        rc.RoleName
    FROM (
        -- Pull from HR via usp_GetUserHRInfo logic (cross-DB view)
        SELECT
            SAMACC        AS SamAcc,
            ISNULL(UEMAIL,'')   AS Email,
            ISNULL(DISPNAME,'') AS FullName,
            ISNULL(dep_code,'') AS DepCode,
            ISNULL(DEPART,'')   AS Department,
            dbo.FUNC_GetInfoByFullName(reporter, 1) AS ManagerSamAcc,
            ISNULL(reporter,'')  AS ManagerName,
            dbo.FUNC_GetInfoByFullName(reporter, 2) AS ManagerEmail
        FROM [BT_HR].[dbo].[onl_TBADUsers]
        WHERE SAMACC != ''
    ) u
    LEFT JOIN [dbo].[dar_UserApprovalRoles] r  ON LOWER(r.SamAcc) = LOWER(u.SamAcc) AND r.IsActive = 1
    LEFT JOIN [dbo].[dar_RoleConfig]        rc ON rc.RoleType = r.RoleType
    ORDER BY u.Department, u.FullName;
END
GO
PRINT 'SP usp_GetAllUserFromAD created.';
GO

-- ────────────────────────────────────────────────────────────
-- 3. usp_GetReviewersByDocType
--    Returns eligible reviewers for a given DocType
--    (users in dar_UserApprovalRoles whose RoleType is in ReviewerRoles)
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_GetReviewersByDocType', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetReviewersByDocType;
GO
CREATE PROCEDURE dbo.usp_GetReviewersByDocType
    @DocType INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Get ReviewerRoles CSV for this DocType
    DECLARE @ReviewerRoles NVARCHAR(50);
    SELECT @ReviewerRoles = ReviewerRoles
    FROM [dbo].[dar_DocTypeFlowConfig]
    WHERE DocType = @DocType;

    IF @ReviewerRoles IS NULL
    BEGIN
        SELECT '' AS SamAcc, '' AS FullName, '' AS Email,
               '' AS DepCode, '' AS Department,
               0  AS RoleType, '' AS RoleName
        WHERE 1=0;  -- empty result
        RETURN;
    END

    SELECT
        u.SamAcc,
        ISNULL(u.FullName, u.SamAcc) AS FullName,
        [dbo].[FUNC_GetInfoBySamAcc](u.SamAcc,1) AS Email,
        u.DepCode,
        u.Depart   AS Department,
        u.RoleType,
        rc.RoleName
    FROM [dbo].[dar_UserApprovalRoles] u
    JOIN [dbo].[dar_RoleConfig] rc ON rc.RoleType = u.RoleType
    WHERE u.IsActive = 1
      AND EXISTS (
            -- Check if user's RoleType is in the ReviewerRoles CSV
            SELECT 1 FROM STRING_SPLIT(@ReviewerRoles, ',')
            WHERE TRIM(value) = CAST(u.RoleType AS NVARCHAR)
          )
    ORDER BY rc.SortOrder, u.FullName;
END
GO
PRINT 'SP usp_GetReviewersByDocType created.';
GO

-- ────────────────────────────────────────────────────────────
-- 4. usp_GetApproverByDocType
--    Returns fixed approver(s) for a given DocType
--    (users with the ApproverFixedRole for this DocType)
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_GetApproverByDocType', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetApproverByDocType;
GO
CREATE PROCEDURE dbo.usp_GetApproverByDocType
    @DocType INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApproverRole INT;
    SELECT @ApproverRole = ApproverFixedRole
    FROM [dbo].[dar_DocTypeFlowConfig]
    WHERE DocType = @DocType;

    SELECT
        u.SamAcc,
        ISNULL(u.FullName, u.SamAcc) AS FullName,
        [dbo].[FUNC_GetInfoBySamAcc](u.SamAcc,1) AS Email,
        u.DepCode,
        u.Depart   AS Department,
        u.RoleType,
        rc.RoleName
    FROM [dbo].[dar_UserApprovalRoles] u
    JOIN [dbo].[dar_RoleConfig] rc ON rc.RoleType = u.RoleType
    WHERE u.IsActive = 1
      AND u.RoleType = @ApproverRole
    ORDER BY u.FullName;
END
GO
PRINT 'SP usp_GetApproverByDocType created.';
GO

-- ────────────────────────────────────────────────────────────
-- 5. usp_CheckCreatorPermission
--    Returns 1 if @SamAcc is allowed to create @DocType, else 0
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_CheckCreatorPermission', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_CheckCreatorPermission;
GO
CREATE PROCEDURE dbo.usp_CheckCreatorPermission
    @DocType INT,
    @SamAcc  NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CreatorRoles NVARCHAR(50);
    SELECT @CreatorRoles = CreatorRoles
    FROM [dbo].[dar_DocTypeFlowConfig]
    WHERE DocType = @DocType;

    -- Role 0 in CreatorRoles means EVERYONE is allowed
    IF CHARINDEX('0', @CreatorRoles) > 0
    BEGIN
        SELECT CAST(1 AS BIT) AS IsAllowed;
        RETURN;
    END

    -- Check if user has any of the required creator roles
    SELECT CAST(
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[dar_UserApprovalRoles] u
            JOIN STRING_SPLIT(@CreatorRoles, ',') s ON TRIM(s.value) = CAST(u.RoleType AS NVARCHAR)
            WHERE LOWER(u.SamAcc) = LOWER(@SamAcc)
              AND u.IsActive = 1
        ) THEN 1 ELSE 0 END
    AS BIT) AS IsAllowed;
END
GO
PRINT 'SP usp_CheckCreatorPermission created.';
GO

-- ────────────────────────────────────────────────────────────
-- 6. usp_SaveUserApprovalRole
--    Admin: assign role to user (upsert)
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_SaveUserApprovalRole', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SaveUserApprovalRole;
GO
CREATE PROCEDURE dbo.usp_SaveUserApprovalRole
    @SamAcc   NVARCHAR(100),
    @FullName NVARCHAR(200),
    @DepCode  NVARCHAR(20),
    @Depart   NVARCHAR(200),
    @RoleType INT,
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[dar_UserApprovalRoles] AS target
    USING (SELECT @SamAcc AS SamAcc, @RoleType AS RoleType) AS src
    ON LOWER(target.SamAcc) = LOWER(src.SamAcc) AND target.RoleType = src.RoleType
    WHEN MATCHED THEN UPDATE SET
        FullName  = @FullName,
        DepCode   = @DepCode,
        Depart    = @Depart,
        IsActive  = @IsActive,
        UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN INSERT (SamAcc, FullName, DepCode, Depart, RoleType, IsActive)
    VALUES (@SamAcc, @FullName, @DepCode, @Depart, @RoleType, @IsActive);

    SELECT @@ROWCOUNT AS AffectedRows;
END
GO
PRINT 'SP usp_SaveUserApprovalRole created.';
GO

-- ────────────────────────────────────────────────────────────
-- 7. usp_DeleteUserApprovalRole
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.usp_DeleteUserApprovalRole', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_DeleteUserApprovalRole;
GO
CREATE PROCEDURE dbo.usp_DeleteUserApprovalRole
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Soft delete
    UPDATE [dbo].[dar_UserApprovalRoles]
    SET IsActive = 0, UpdatedAt = GETDATE()
    WHERE Id = @Id;
    SELECT @@ROWCOUNT AS AffectedRows;
END
GO
PRINT 'SP usp_DeleteUserApprovalRole created.';
GO

-- ────────────────────────────────────────────────────────────
-- Add Reviewer/Approver columns to dar_Master if not exist
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerSamAcc')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerSamAcc] NVARCHAR(100) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerName')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerName]   NVARCHAR(200) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewerEmail')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewerEmail]  NVARCHAR(200) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ReviewedDate')
    ALTER TABLE [dbo].[dar_Master] ADD [ReviewedDate]   DATETIME NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverSamAcc')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverSamAcc] NVARCHAR(100) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverName')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverName]   NVARCHAR(200) NOT NULL DEFAULT '';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='ApproverEmail')
    ALTER TABLE [dbo].[dar_Master] ADD [ApproverEmail]  NVARCHAR(200) NOT NULL DEFAULT '';
PRINT 'dar_Master columns added.';
GO

PRINT '=== Migration 2.0.0 Complete ===';
GO
