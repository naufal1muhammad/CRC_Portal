CREATE PROCEDURE [dbo].[spStaffDocument_List]
    @Staff_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        [StaffDocument_ID],
        [Staff_ID],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[StaffDocument]
    WHERE [Staff_ID] = @Staff_ID
    ORDER BY [UploadedOn] DESC, [StaffDocument_ID] DESC;
END;