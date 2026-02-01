CREATE PROCEDURE [dbo].[spLU_LOCATION_ListPostcodesByCity]
    @CityId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [LocationId],
           [ParentId],
           [Name],
           [SortOrder]
    FROM [dbo].[LU_LOCATION]
    WHERE [LocationType] = 3
      AND [ParentId] = @CityId
    ORDER BY COALESCE([SortOrder], 2147483647),
             [Name];
END;
