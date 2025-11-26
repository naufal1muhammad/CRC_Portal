CREATE PROCEDURE [dbo].[spStaffDocument_Insert]
    @Staff_ID    VARCHAR(100),
    @FileName    VARCHAR(255),
    @FilePath    VARCHAR(500),
    @ContentType VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[StaffDocument] (
        [Staff_ID],
        [FileName],
        [FilePath],
        [ContentType]
    )
    VALUES (
        @Staff_ID,
        @FileName,
        @FilePath,
        @ContentType
    );
END;