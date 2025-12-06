CREATE PROCEDURE [dbo].[spPatientDocument_List]
	@Patient_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [PatientDocument_ID],
        [Patient_ID],
        [Patient_Name],
        [PatientDocumentType_ID],
        [PatientDocumentType_Name],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[PatientDocument]
    WHERE [Patient_ID] = @Patient_ID
    ORDER BY [UploadedOn] DESC, [PatientDocument_ID] DESC;
END;
GO
