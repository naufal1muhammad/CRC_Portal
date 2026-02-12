CREATE PROCEDURE [dbo].[spPatientDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH src AS
    (
        SELECT
            NULLIF(LTRIM(RTRIM(ISNULL([PatientDocumentType_ID], ''))), '')   AS [PatientDocumentType_ID],
            NULLIF(LTRIM(RTRIM(ISNULL([PatientDocumentType_Name], ''))), '') AS [PatientDocumentType_Name]
        FROM [dbo].[LU_PATDOCUMENTTYPE]

        UNION

        SELECT
            NULLIF(LTRIM(RTRIM(ISNULL([PatientDocumentType_ID], ''))), '')   AS [PatientDocumentType_ID],
            NULLIF(LTRIM(RTRIM(ISNULL([PatientDocumentType_Name], ''))), '') AS [PatientDocumentType_Name]
        FROM [dbo].[PatientDocument]
    )
    SELECT DISTINCT
        [PatientDocumentType_ID],
        -- Fallback to ID if Name is missing (keeps the dropdown usable for older/bad data)
        COALESCE([PatientDocumentType_Name], [PatientDocumentType_ID]) AS [PatientDocumentType_Name]
    FROM src
    WHERE COALESCE([PatientDocumentType_Name], [PatientDocumentType_ID]) IS NOT NULL
    ORDER BY COALESCE([PatientDocumentType_Name], [PatientDocumentType_ID]);
END;
GO
