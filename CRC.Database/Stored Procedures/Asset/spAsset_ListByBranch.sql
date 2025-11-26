CREATE PROCEDURE [dbo].[spAsset_ListByBranch]
    @Branch_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Asset_ID],
        [Asset_Name],
        [Branch_ID],
        [Branch_Name],
        [Asset_Quantity],
        [Asset_Cost],
        [Asset_TotalCost]
    FROM [dbo].[Asset]
    WHERE [Branch_ID] = @Branch_ID
    ORDER BY [Asset_Name];
END;