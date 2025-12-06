CREATE PROCEDURE [dbo].[spLU_Occupation_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Occupation_ID], [Occupation_Name]
    FROM [dbo].[LU_OCCUPATION]
    ORDER BY [Occupation_Name];
END;
GO