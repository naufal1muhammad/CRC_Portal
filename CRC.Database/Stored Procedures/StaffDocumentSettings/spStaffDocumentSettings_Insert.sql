CREATE PROCEDURE [dbo].[spStaffDocumentSettings_Insert]
    @StaffType_ID           VARCHAR(100),
    @StaffType_Name         VARCHAR(100),
    @StaffDocumentType_ID   VARCHAR(100),
    @StaffDocumentType_Name VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[StaffDocumentSettings]
    (
        [StaffType_ID],
        [StaffType_Name],
        [StaffDocumentType_ID],
        [StaffDocumentType_Name]
    )
    VALUES
    (
        @StaffType_ID,
        @StaffType_Name,
        @StaffDocumentType_ID,
        @StaffDocumentType_Name
    );
END;