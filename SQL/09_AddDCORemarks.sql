-- ============================================================
-- Migration: Add DCORemarks column to dar_Master
-- Version  : 3.1.0
-- ============================================================

USE BT_QCDAR;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dar_Master') AND name = 'DCORemarks'
)
    ALTER TABLE [dbo].[dar_Master]
        ADD [DCORemarks] NVARCHAR(MAX) NULL;

PRINT 'DCORemarks column added.';
GO
PRINT '=== Migration 3.1.0 Complete ===';
GO
