CREATE PROCEDURE [dbo].[spAsset_GetById]
    @Asset_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [Asset_ID],
        [Asset_Name],
        [Branch_ID],
        [Branch_Name],
        [Asset_Quantity],
        [Asset_Cost],
        [Asset_TotalCost]
    FROM [dbo].[Asset]
    WHERE [Asset_ID] = @Asset_ID;
END;