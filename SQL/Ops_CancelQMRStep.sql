-- ============================================================
-- One-off: cancel the QMR step of a DAR so QMR can act again
--          (e.g. DCC agreed/signed in place of QMR by mistake)
-- Usage  : set @DarNo, run the whole script. Review the BEFORE
--          result, then the script commits only if exactly 1 row
--          was changed; otherwise it rolls back.
-- Scope  : QMR step only (MR* columns). Status stays PendingDCO(3).
--          Refuses to run once DCC has completed the DAR.
-- ============================================================
USE BT_QCDAR;
GO

DECLARE @DarNo NVARCHAR(20) = N'DAR-2026-XXXXX';   -- <<< set this

-- BEFORE
SELECT DarNo, Status, MRAgree, MRSamAcc, MRDate, MRSignedAt, MRCertThumbprint,
       DCOSamAcc, DocRegisteredDate, DCOSignedAt, Remarks
FROM   [dbo].[dar_Master]
WHERE  DarNo = @DarNo;

BEGIN TRAN;

UPDATE [dbo].[dar_Master]
SET    MRAgree           = NULL,
       MRSamAcc          = NULL,
       MRDate            = NULL,
       MRSignedAt        = NULL,
       MRSignatureBase64 = NULL,
       MRCertThumbprint  = NULL,
       Status            = 3,            -- PendingDCO: QMR acts again from here
       UpdatedAt         = GETDATE()
       -- , Remarks = NULL               -- uncomment to also clear the QMR remark
WHERE  DarNo = @DarNo
  AND  Status = 3                        -- still PendingDCO
  AND  DocRegisteredDate IS NULL         -- DCC has not completed it
  AND  DCOSignedAt IS NULL;

IF @@ROWCOUNT = 1
BEGIN
    COMMIT;
    PRINT 'QMR step cancelled for ' + @DarNo;
END
ELSE
BEGIN
    ROLLBACK;
    PRINT 'Nothing changed (DarNo not found, not PendingDCO, or DCC already completed). Rolled back.';
END

-- AFTER
SELECT DarNo, Status, MRAgree, MRSamAcc, MRDate, MRSignedAt, DocRegisteredDate, Remarks
FROM   [dbo].[dar_Master]
WHERE  DarNo = @DarNo;
GO
