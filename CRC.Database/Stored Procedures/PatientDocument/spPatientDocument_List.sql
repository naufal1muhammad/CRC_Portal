CREATE PROCEDURE [dbo].[spPatientDocument_List]
    @Patient_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pd.[PatientDocument_ID],
        pd.[Patient_ID],
        pb.[Patient_Name] AS [Patient_Name],
        pd.[PatientDocumentType_ID],
        COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), pd.[PatientDocumentType_ID]) AS [PatientDocumentType_Name],
        pd.[FileName],
        pd.[FilePath],
        pd.[ContentType],
        pd.[UploadedOn]
    FROM [dbo].[PatientDocument] pd
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pd.[Patient_ID]
    LEFT JOIN [dbo].[LU_PATDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(pd.[PatientDocumentType_ID], ''))))
    WHERE pd.[Patient_ID] = @Patient_ID
    ORDER BY pd.[UploadedOn] DESC, pd.[PatientDocument_ID] DESC;
END;
GO