CREATE PROCEDURE [dbo].[spLU_LOCATION_ListPostcodesByDistrict]
    @DistrictId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [LocationId],
           [ParentId],
           [Name],
           [SortOrder]
    FROM [dbo].[LU_LOCATION]
    WHERE [LocationType] = 3
      AND [ParentId] = @DistrictId
    ORDER BY COALESCE([SortOrder], 2147483647),
             [Name];
END;
