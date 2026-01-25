CREATE PROCEDURE [dbo].[spLU_LOCATION_ListStates]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [LocationId],
           [Name],
           [SortOrder]
    FROM [dbo].[LU_LOCATION]
    WHERE [LocationType] = 1
    ORDER BY COALESCE([SortOrder], 2147483647),
             [Name];
END;
