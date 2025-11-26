CREATE PROCEDURE [dbo].[spAsset_Insert]
    @Asset_ID        VARCHAR(100),
    @Asset_Name      VARCHAR(100),
    @Branch_ID       VARCHAR(100),
    @Branch_Name     VARCHAR(100),
    @Asset_Quantity  INT,
    @Asset_Cost      DECIMAL(18, 2)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Asset] WHERE [Asset_ID] = @Asset_ID)
    BEGIN
        RAISERROR('Asset ID already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Asset] (
        [Asset_ID],
        [Asset_Name],
        [Branch_ID],
        [Branch_Name],
        [Asset_Quantity],
        [Asset_Cost]
        -- Asset_TotalCost is computed
    )
    VALUES (
        @Asset_ID,
        @Asset_Name,
        @Branch_ID,
        @Branch_Name,
        @Asset_Quantity,
        @Asset_Cost
    );
END;