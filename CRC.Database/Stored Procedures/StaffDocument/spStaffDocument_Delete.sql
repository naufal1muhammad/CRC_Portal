CREATE PROCEDURE [dbo].[spStaffDocument_Delete]
    @StaffDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[StaffDocument]
    WHERE [StaffDocument_ID] = @StaffDocument_ID;
END;