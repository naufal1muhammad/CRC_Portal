CREATE PROCEDURE [dbo].[spStaffDocument_Search]
    @StaffDocumentType_ID VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [StaffDocument_ID],
        [Staff_ID],
        [Staff_Name],
        [StaffDocumentType_ID],
        [StaffDocumentType_Name],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[StaffDocument]
    WHERE
        (@StaffDocumentType_ID IS NULL OR @StaffDocumentType_ID = '' OR
         [StaffDocumentType_ID] = @StaffDocumentType_ID)
    ORDER BY [UploadedOn] DESC, [StaffDocument_ID] DESC;
END;