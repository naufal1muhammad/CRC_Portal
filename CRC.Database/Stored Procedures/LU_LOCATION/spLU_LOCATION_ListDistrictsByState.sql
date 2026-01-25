CREATE PROCEDURE [dbo].[spLU_LOCATION_ListDistrictsByState]
    @StateId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [LocationId],
           [ParentId],
           [Name],
           [SortOrder]
    FROM [dbo].[LU_LOCATION]
    WHERE [LocationType] = 2
      AND [ParentId] = @StateId
    ORDER BY COALESCE([SortOrder], 2147483647),
             [Name];
END;
