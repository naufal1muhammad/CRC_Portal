CREATE PROCEDURE [dbo].[spPatientDocument_Insert]
(
    @Patient_ID               VARCHAR(100),
    @Patient_Name             VARCHAR(100),
    @PatientDocumentType_ID   VARCHAR(100),
    @PatientDocumentType_Name VARCHAR(100),
    @FileName                 VARCHAR(255),
    @FilePath                 VARCHAR(500),
    @ContentType              VARCHAR(100),
    @User_ID                  INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[PatientDocument]
    (
        [Patient_ID],
        [PatientDocumentType_ID],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    )
    VALUES
    (
        @Patient_ID,
        @PatientDocumentType_ID,
        @FileName,
        @FilePath,
        @ContentType,
        CONVERT(VARCHAR(100), GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time', 120)
    );

    DECLARE @InsertedPatientDocumentId INT = CONVERT(INT, SCOPE_IDENTITY());

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
        'INSERT',
        'PatientDocument',
        CONCAT(
            'Uploaded Patient Document: PatientDocument_ID=', @InsertedPatientDocumentId,
            '; Patient_ID=', @Patient_ID,
            '; Patient_Name=', ISNULL(@Patient_Name, ''),
            '; DocType=', ISNULL(@PatientDocumentType_Name, ''), ' (', ISNULL(@PatientDocumentType_ID, ''), ')',
            '; FileName=', @FileName,
            '; ContentType=', @ContentType
        )
    );
END;
GO