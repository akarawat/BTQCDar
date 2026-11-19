-- ============================================================
-- Migration: Widen dar_Master.DocumentNo  NVARCHAR(50) -> NVARCHAR(500)
-- Reason   : A DAR may cover many document numbers
--            (e.g. "7380167001,7380167002,...,7380167050") and the
--            old 50-char limit made DAR creation fail with
--            "String or binary data would be truncated".
-- Safe     : idempotent (only alters if currently < 500 chars);
--            widening NVARCHAR keeps the NOT NULL + DEFAULT '' and
--            existing data. dar_Master.DocumentNo has no index.
-- ============================================================
USE BT_QCDAR;
GO

-- Pre-check: any dependency that could block / be affected by the ALTER?
-- (indexes or computed columns on DocumentNo; stored procs/views that mention it)
SELECT 'INDEX' AS Kind, i.name AS ObjName
FROM   sys.index_columns ic
JOIN   sys.indexes i  ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN   sys.columns c  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE  ic.object_id = OBJECT_ID('dbo.dar_Master') AND c.name = 'DocumentNo'
UNION ALL
SELECT 'MODULE', OBJECT_SCHEMA_NAME(m.object_id) + '.' + OBJECT_NAME(m.object_id)
FROM   sys.sql_modules m
WHERE  m.definition LIKE '%DocumentNo%';
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.dar_Master') AND name = 'DocumentNo'
      AND max_length <> -1 AND max_length < 1000          -- NVARCHAR(500) = 1000 bytes
)
BEGIN
    -- Keep the existing default constraint (DEFAULT '') — ALTER COLUMN does not drop it
    -- for a plain length change.
    ALTER TABLE [dbo].[dar_Master] ALTER COLUMN [DocumentNo] NVARCHAR(500) NOT NULL;
    PRINT 'dar_Master.DocumentNo widened to NVARCHAR(500).';
END
ELSE
    PRINT 'dar_Master.DocumentNo already >= 500 chars — nothing to do.';
GO

-- Verify
SELECT c.name, t.name AS type, c.max_length / 2 AS max_chars, c.is_nullable
FROM   sys.columns c
JOIN   sys.types   t ON t.user_type_id = c.user_type_id
WHERE  c.object_id = OBJECT_ID('dbo.dar_Master') AND c.name IN ('DocumentNo','DocumentName');
GO
PRINT '=== Migration 16 (DocumentNo 500) Complete ===';
GO
