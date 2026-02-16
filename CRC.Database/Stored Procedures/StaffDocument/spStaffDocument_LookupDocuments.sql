CREATE PROCEDURE [dbo].[spStaffDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH docTypes AS
    (
        SELECT DISTINCT
            NULLIF(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], ''))), '') AS [StaffDocumentType_ID]
        FROM [dbo].[StaffDocument] d
        WHERE NULLIF(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], ''))), '') IS NOT NULL

        UNION

        SELECT DISTINCT
            NULLIF(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], ''))), '') AS [StaffDocumentType_ID]
        FROM [dbo].[LU_STAFFDOCUMENTTYPE] t
        WHERE NULLIF(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], ''))), '') IS NOT NULL
    )
    SELECT
        dt.[StaffDocumentType_ID],
        COALESCE(NULLIF(LTRIM(RTRIM(t.[StaffDocumentType_Name])), ''), dt.[StaffDocumentType_ID]) AS [StaffDocumentType_Name]
    FROM docTypes dt
    LEFT JOIN [dbo].[LU_STAFFDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], '')))) = UPPER(dt.[StaffDocumentType_ID])
    ORDER BY COALESCE(NULLIF(LTRIM(RTRIM(t.[StaffDocumentType_Name])), ''), dt.[StaffDocumentType_ID]);
END;
GO