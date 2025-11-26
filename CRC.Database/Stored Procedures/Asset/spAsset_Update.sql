CREATE PROCEDURE [dbo].[spAsset_Update]
    @Asset_ID        VARCHAR(100),
    @Asset_Name      VARCHAR(100),
    @Branch_ID       VARCHAR(100),
    @Branch_Name     VARCHAR(100),
    @Asset_Quantity  INT,
    @Asset_Cost      DECIMAL(18, 2)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Asset] WHERE [Asset_ID] = @Asset_ID)
    BEGIN
        RAISERROR('Asset not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Asset]
    SET
        [Asset_Name]     = @Asset_Name,
        [Branch_ID]      = @Branch_ID,
        [Branch_Name]    = @Branch_Name,
        [Asset_Quantity] = @Asset_Quantity,
        [Asset_Cost]     = @Asset_Cost
    WHERE [Asset_ID] = @Asset_ID;
END;