CREATE PROCEDURE [dbo].[spStaffDocumentSettings_DeleteByStaffType]
    @StaffType_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[StaffDocumentSettings]
    WHERE [StaffType_ID] = @StaffType_ID;
END;