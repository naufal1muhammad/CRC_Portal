CREATE PROCEDURE [dbo].[spLU_STAFFDOCUMENTTYPE_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [StaffDocumentType_ID],
        [StaffDocumentType_Name]
    FROM [dbo].[LU_STAFFDOCUMENTTYPE]
    ORDER BY [StaffDocumentType_Name];
END;