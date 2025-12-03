CREATE PROCEDURE [dbo].[spStaffDocument_Insert]
    @Staff_ID             VARCHAR(100),
    @Staff_Name           VARCHAR(100),
    @StaffDocumentType_ID   VARCHAR(100),
    @StaffDocumentType_Name VARCHAR(100),
    @FileName             VARCHAR(255),
    @FilePath             VARCHAR(500),
    @ContentType          VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[StaffDocument]
    (
        [Staff_ID],
        [Staff_Name],
        [StaffDocumentType_ID],
        [StaffDocumentType_Name],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    )
    VALUES
    (
        @Staff_ID,
        @Staff_Name,
        @StaffDocumentType_ID,
        @StaffDocumentType_Name,
        @FileName,
        @FilePath,
        @ContentType,
        GETDATE()
    );
END;