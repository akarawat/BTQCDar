-- ============================================================
-- BT_QCDAR Database Setup Script
-- Project : BTQCDar — Document Action Request System
-- Version : 1.0.0
-- Date    : 2025
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'BT_QCDAR')
BEGIN
    CREATE DATABASE BT_QCDAR
        COLLATE THAI_CI_AS;
    PRINT 'Database BT_QCDAR created.';
END
GO

USE BT_QCDAR;
GO

-- ────────────────────────────────────────────────────────────
-- 1. dar_Master  (main DAR record)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'dar_Master')
BEGIN
    CREATE TABLE [dbo].[dar_Master] (
        [DarId]                 INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DarNo]                 NVARCHAR(20)  NOT NULL UNIQUE,          -- DAR-YYYY-XXXXX

        -- Type section
        [DocType]               INT           NOT NULL DEFAULT 1,       -- DarDocType enum
        [DocTypeOther]          NVARCHAR(200) NOT NULL DEFAULT '',

        -- For section (standard)
        [ForStandard]           INT           NOT NULL DEFAULT 1,       -- DarForStandard enum
        [ForStandardOther]      NVARCHAR(200) NOT NULL DEFAULT '',

        -- Document info
        [DocumentNo]            NVARCHAR(50)  NOT NULL DEFAULT '',
        [DocumentName]          NVARCHAR(500) NOT NULL DEFAULT '',

        -- Purpose section
        [Purpose]               INT           NOT NULL DEFAULT 1,       -- DarPurpose enum
        [PurposeOther]          NVARCHAR(200) NOT NULL DEFAULT '',

        -- Content
        [Content]               NVARCHAR(MAX) NOT NULL DEFAULT '',
        [HasAttachment]         BIT           NOT NULL DEFAULT 0,

        -- Status of document under request (free text)
        [DocStatusUnderRequest] NVARCHAR(200) NOT NULL DEFAULT '',

        -- Reason
        [ReasonBehindPurpose]   NVARCHAR(MAX) NOT NULL DEFAULT '',

        -- Effective date & revision
        [EffectiveDate]         DATE          NULL,
        [RevisionNo]            NVARCHAR(20)  NOT NULL DEFAULT '',

        -- Distribution
        [IsControlledCopy]      BIT           NOT NULL DEFAULT 1,
        [IsUncontrolledCopy]    BIT           NOT NULL DEFAULT 0,
        [DistributionList]      NVARCHAR(MAX) NOT NULL DEFAULT '',

        -- Requestor
        [RequestedBySamAcc]     NVARCHAR(100) NOT NULL,
        [RequestedByName]       NVARCHAR(200) NOT NULL,
        [RequestedDate]         DATETIME      NOT NULL DEFAULT GETDATE(),

        -- Approver
        [ApprovedBySamAcc]      NVARCHAR(100) NULL,
        [ApprovedByName]        NVARCHAR(200) NULL,
        [ApprovedDate]          DATETIME      NULL,

        -- MR
        [MRAgree]               BIT           NULL,
        [MRSamAcc]              NVARCHAR(100) NULL,
        [MRDate]                DATETIME      NULL,

        -- DCO
        [DCOSamAcc]             NVARCHAR(100) NULL,
        [DocRegisteredDate]     DATETIME      NULL,

        -- Workflow
        [Status]                INT           NOT NULL DEFAULT 0,       -- DarStatus enum
        [Remarks]               NVARCHAR(MAX) NOT NULL DEFAULT '',

        -- Audit
        [CreatedAt]             DATETIME      NOT NULL DEFAULT GETDATE(),
        [UpdatedAt]             DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Table dar_Master created.';
END
GO

-- ────────────────────────────────────────────────────────────
-- 2. dar_Distribution  (Controlled Copy distribution record)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'dar_Distribution')
BEGIN
    CREATE TABLE [dbo].[dar_Distribution] (
        [DistId]       INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DarId]        INT           NOT NULL REFERENCES [dar_Master]([DarId]),
        [DeptSect]     NVARCHAR(100) NOT NULL DEFAULT '',
        [ReceiveSign]  NVARCHAR(100) NOT NULL DEFAULT '',
        [ReceiveDate]  DATETIME      NULL,
        [ReturnSign]   NVARCHAR(100) NOT NULL DEFAULT '',
        [ReturnDate]   DATETIME      NULL,
        [SortOrder]    INT           NOT NULL DEFAULT 0
    );
    PRINT 'Table dar_Distribution created.';
END
GO

-- ────────────────────────────────────────────────────────────
-- 3. dar_RelatedDoc  (Related Documents section)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'dar_RelatedDoc')
BEGIN
    CREATE TABLE [dbo].[dar_RelatedDoc] (
        [RelDocId]     INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DarId]        INT           NOT NULL REFERENCES [dar_Master]([DarId]),
        [ItemNo]       INT           NOT NULL DEFAULT 1,
        [DocumentNo]   NVARCHAR(100) NOT NULL DEFAULT '',
        [DocumentName] NVARCHAR(500) NOT NULL DEFAULT '',
        [IsRevise]     BIT           NOT NULL DEFAULT 0,
        [IsKeepAsIs]   BIT           NOT NULL DEFAULT 0
    );
    PRINT 'Table dar_RelatedDoc created.';
END
GO

-- ────────────────────────────────────────────────────────────
-- 4. dar_UserRoles  (DAR-specific role assignments)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'dar_UserRoles')
BEGIN
    CREATE TABLE [dbo].[dar_UserRoles] (
        [RoleId]     INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SamAcc]     NVARCHAR(100) NOT NULL UNIQUE,
        [FullName]   NVARCHAR(200) NOT NULL DEFAULT '',
        [IsApprover] BIT           NOT NULL DEFAULT 0,
        [IsMR]       BIT           NOT NULL DEFAULT 0,
        [IsDCO]      BIT           NOT NULL DEFAULT 0,
        [IsAdmin]    BIT           NOT NULL DEFAULT 0,
        [UpdatedAt]  DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Table dar_UserRoles created.';
END
GO

-- ────────────────────────────────────────────────────────────
-- 5. Seed: Admin user (replace with actual SAM account)
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM [dbo].[dar_UserRoles] WHERE SamAcc = 'admin')
BEGIN
    INSERT INTO [dbo].[dar_UserRoles] (SamAcc, FullName, IsApprover, IsMR, IsDCO, IsAdmin)
    VALUES ('admin', 'System Admin', 1, 1, 1, 1);
    PRINT 'Seed admin role inserted.';
END
GO

PRINT '=== BT_QCDAR Setup Complete ===';
GO
