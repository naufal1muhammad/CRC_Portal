CREATE PROCEDURE [dbo].[spPatientDocument_Delete]
(
    @PatientDocument_ID INT,
    @User_ID            INT = NULL,
    -- The caller uses this to delete the corresponding BLOB after the row is gone.
    -- A NULL means no row was deleted and nothing should be removed from storage.
    @DeletedBlobName    VARCHAR(500) = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Capture details for audit (best effort)
    DECLARE
        @Patient_ID VARCHAR(100),
        @Patient_Name VARCHAR(100),
        @PatientDocumentType_ID VARCHAR(100),
        @PatientDocumentType_Name VARCHAR(100),
        @FileName VARCHAR(255),
        @BlobName VARCHAR(500),
        @ContentType VARCHAR(100),
        @UploadedOn VARCHAR(100);

    SELECT
        @Patient_ID = [Patient_ID],
        @PatientDocumentType_ID = [PatientDocumentType_ID],
        @FileName = [FileName],
        @BlobName = [BlobName],
        @ContentType = [ContentType],
        @UploadedOn = [UploadedOn]
    FROM [dbo].[PatientDocument]
    WHERE [PatientDocument_ID] = @PatientDocument_ID;

    -- Lookups (best effort)
    SELECT
        @Patient_Name = [Patient_Name]
    FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;

    SELECT
        @PatientDocumentType_Name = [PatientDocumentType_Name]
    FROM [dbo].[LU_PATDOCUMENTTYPE]
    WHERE [PatientDocumentType_ID] = @PatientDocumentType_ID;

    DELETE FROM [dbo].[PatientDocument]
    WHERE [PatientDocument_ID] = @PatientDocument_ID;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    SET @DeletedBlobName = CASE WHEN @RowsAffected > 0 THEN @BlobName ELSE NULL END;

    IF @RowsAffected > 0
    BEGIN
        -- -----------------------------
        -- Audit trail
        -- -----------------------------
        INSERT INTO [dbo].[AuditTrails]
        (
            [User_Id],
            [AuditTrail_Action],
            [AuditTrail_Category],
            [AuditTrail_Summary]
        )
        VALUES
        (
            ISNULL(@User_ID, 0),
            'DELETE',
            'PatientDocument',
            CONCAT(
                'Deleted Patient Document: PatientDocument_ID=', @PatientDocument_ID,
                '; Patient_ID=', ISNULL(@Patient_ID, ''),
                '; Patient_Name=', ISNULL(@Patient_Name, ''),
                '; DocType=', ISNULL(@PatientDocumentType_Name, ''), ' (', ISNULL(@PatientDocumentType_ID, ''), ')',
                '; FileName=', ISNULL(@FileName, ''),
                '; ContentType=', ISNULL(@ContentType, ''),
                '; UploadedOn=', ISNULL(@UploadedOn, '')
            )
        );
    END
END;
GO