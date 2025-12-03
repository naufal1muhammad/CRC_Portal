CREATE PROCEDURE [dbo].[spStaffDocument_List]
    @Staff_ID VARCHAR(100) = NULL
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
    WHERE (@Staff_ID IS NULL OR @Staff_ID = '' OR [Staff_ID] = @Staff_ID)
    ORDER BY [UploadedOn] DESC, [StaffDocument_ID] DESC;
END;