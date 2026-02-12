CREATE PROCEDURE [dbo].[spPatientDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH docTypes AS
    (
        SELECT DISTINCT
            NULLIF(LTRIM(RTRIM(ISNULL(pd.[PatientDocumentType_ID], ''))), '') AS [PatientDocumentType_ID]
        FROM [dbo].[PatientDocument] pd
        WHERE NULLIF(LTRIM(RTRIM(ISNULL(pd.[PatientDocumentType_ID], ''))), '') IS NOT NULL

        UNION

        SELECT DISTINCT
            NULLIF(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], ''))), '') AS [PatientDocumentType_ID]
        FROM [dbo].[LU_PATDOCUMENTTYPE] t
        WHERE NULLIF(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], ''))), '') IS NOT NULL
    )
    SELECT
        dt.[PatientDocumentType_ID],
        COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), dt.[PatientDocumentType_ID]) AS [PatientDocumentType_Name]
    FROM docTypes dt
    LEFT JOIN [dbo].[LU_PATDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], '')))) = UPPER(dt.[PatientDocumentType_ID])
    ORDER BY COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), dt.[PatientDocumentType_ID]);
END;
GO