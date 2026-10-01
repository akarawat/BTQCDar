-- ============================================================
-- Migration: Widen dar_RelatedDoc columns to match UI spec
--            (DocumentNo 100->200, DocumentName 500->512)
-- Table already exists since 01_CreateDatabase_BT_QCDAR.sql —
-- this just widens two columns, safe/idempotent to re-run.
-- Version  : 3.6.0
-- ============================================================
USE BT_QCDAR;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dar_RelatedDoc') AND c.name = 'DocumentNo' AND c.max_length < 400
)
    ALTER TABLE [dbo].[dar_RelatedDoc] ALTER COLUMN [DocumentNo] NVARCHAR(200) NOT NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dar_RelatedDoc') AND c.name = 'DocumentName' AND c.max_length < 1024
)
    ALTER TABLE [dbo].[dar_RelatedDoc] ALTER COLUMN [DocumentName] NVARCHAR(512) NOT NULL;
GO

PRINT 'dar_RelatedDoc columns widened (DocumentNo=200, DocumentName=512).';
GO
PRINT '=== Migration 3.6.0 Complete ===';
GO
