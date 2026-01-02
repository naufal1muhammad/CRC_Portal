CREATE PROCEDURE [dbo].[spLU_Source_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Source_ID], [Source_Name]
    FROM [dbo].[LU_SOURCE]
    ORDER BY [Source_Name];
END;
GO