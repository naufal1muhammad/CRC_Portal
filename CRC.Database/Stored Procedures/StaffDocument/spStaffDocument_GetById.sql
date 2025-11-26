CREATE PROCEDURE [dbo].[spStaffDocument_GetById]
    @StaffDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [StaffDocument_ID],
        [Staff_ID],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    FROM [dbo].[StaffDocument]
    WHERE [StaffDocument_ID] = @StaffDocument_ID;
END;