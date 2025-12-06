CREATE PROCEDURE [dbo].[spPatientDocument_Insert]
(
    @Patient_ID               VARCHAR(100),
    @Patient_Name             VARCHAR(100),
    @PatientDocumentType_ID   VARCHAR(100),
    @PatientDocumentType_Name VARCHAR(100),
    @FileName                 VARCHAR(255),
    @FilePath                 VARCHAR(500),
    @ContentType              VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[PatientDocument]
    (
        [Patient_ID],
        [Patient_Name],
        [PatientDocumentType_ID],
        [PatientDocumentType_Name],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    )
    VALUES
    (
        @Patient_ID,
        @Patient_Name,
        @PatientDocumentType_ID,
        @PatientDocumentType_Name,
        @FileName,
        @FilePath,
        @ContentType,
        CONVERT(VARCHAR(100), GETDATE(), 120)
    );
END;
GO