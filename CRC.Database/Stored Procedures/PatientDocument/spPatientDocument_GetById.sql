-- Single-row read for one patient document.
-- It exists so the app can resolve one document's blob key and original filename
-- in order to mint a short-lived read SAS for the download.
-- Read-only: no @User_ID and no audit row -- the DOWNLOAD is audited by the app
-- on the Serilog audit channel, not here.
CREATE PROCEDURE [dbo].[spPatientDocument_GetById]
    @PatientDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        pd.[PatientDocument_ID],
        pd.[Patient_ID],
        pb.[Patient_Name],
        pd.[PatientDocumentType_ID],
        COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), pd.[PatientDocumentType_ID]) AS [PatientDocumentType_Name],
        pd.[FileName],
        pd.[BlobName],
        pd.[ContentType],
        pd.[UploadedOn]
    FROM [dbo].[PatientDocument] pd
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pd.[Patient_ID]
    LEFT JOIN [dbo].[LU_PATDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(pd.[PatientDocumentType_ID], ''))))
    WHERE pd.[PatientDocument_ID] = @PatientDocument_ID;
END;
GO
