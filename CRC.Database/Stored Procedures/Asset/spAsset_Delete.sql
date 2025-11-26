CREATE PROCEDURE [dbo].[spAsset_Delete]
    @Asset_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Asset]
    WHERE [Asset_ID] = @Asset_ID;
END;