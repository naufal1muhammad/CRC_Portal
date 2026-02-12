CREATE PROCEDURE [dbo].[spStaffDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        StaffDocumentType_Name
    FROM [dbo].[StaffDocument]
    WHERE ISNULL(LTRIM(RTRIM(StaffDocumentType_Name)), '') <> ''
    ORDER BY StaffDocumentType_Name;
END;
GO
