-- ============================================================
-- Migration: Digital Signature columns for QMR and DCC
-- Version  : 3.2.0
-- ============================================================
USE BT_QCDAR;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='MRSignedAt')
    ALTER TABLE [dbo].[dar_Master] ADD [MRSignedAt]          DATETIME      NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='MRSignatureBase64')
    ALTER TABLE [dbo].[dar_Master] ADD [MRSignatureBase64]   NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='MRCertThumbprint')
    ALTER TABLE [dbo].[dar_Master] ADD [MRCertThumbprint]    NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='DCOSignedAt')
    ALTER TABLE [dbo].[dar_Master] ADD [DCOSignedAt]         DATETIME      NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='DCOSignatureBase64')
    ALTER TABLE [dbo].[dar_Master] ADD [DCOSignatureBase64]  NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dar_Master') AND name='DCOCertThumbprint')
    ALTER TABLE [dbo].[dar_Master] ADD [DCOCertThumbprint]   NVARCHAR(200) NULL;

PRINT 'MR + DCO signature columns added.';
GO
PRINT '=== Migration 3.2.0 Complete ===';
GO
