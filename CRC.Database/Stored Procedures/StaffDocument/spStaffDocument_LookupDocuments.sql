CREATE PROCEDURE [dbo].[spStaffDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH src AS
    (
        SELECT
            NULLIF(LTRIM(RTRIM(ISNULL([StaffDocumentType_ID], ''))), '')   AS [StaffDocumentType_ID],
            NULLIF(LTRIM(RTRIM(ISNULL([StaffDocumentType_Name], ''))), '') AS [StaffDocumentType_Name]
        FROM [dbo].[StaffDocument]
    )
    SELECT DISTINCT
        [StaffDocumentType_ID],
        -- Fallback to ID if Name is missing (keeps the dropdown usable for older/bad data)
        COALESCE([StaffDocumentType_Name], [StaffDocumentType_ID]) AS [StaffDocumentType_Name]
    FROM src
    WHERE COALESCE([StaffDocumentType_Name], [StaffDocumentType_ID]) IS NOT NULL
    ORDER BY COALESCE([StaffDocumentType_Name], [StaffDocumentType_ID]);
END;
GO