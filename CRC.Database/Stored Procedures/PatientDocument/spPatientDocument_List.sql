CREATE PROCEDURE [dbo].[spPatientDocument_List]
    @Patient_ID VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        [PatientDocument_ID],
        [Patient_ID],
        [Patient_Name],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[PatientDocument]
    WHERE (@Patient_ID IS NULL OR @Patient_ID = '' OR [Patient_ID] = @Patient_ID)
    ORDER BY [UploadedOn] DESC, [PatientDocument_ID] DESC;
END;