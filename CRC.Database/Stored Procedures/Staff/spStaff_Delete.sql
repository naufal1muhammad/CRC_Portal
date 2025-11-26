CREATE PROCEDURE [dbo].[spStaff_Delete]
    @Staff_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;
END;