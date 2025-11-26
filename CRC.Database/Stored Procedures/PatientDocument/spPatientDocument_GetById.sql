CREATE PROCEDURE [dbo].[spPatientDocument_GetById]
    @PatientDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [PatientDocument_ID],
        [Patient_ID],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[PatientDocument]
    WHERE [PatientDocument_ID] = @PatientDocument_ID;
END;