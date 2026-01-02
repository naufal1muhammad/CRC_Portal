CREATE PROCEDURE [dbo].[spLU_Race_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Race_ID], [Race_Name]
    FROM [dbo].[LU_RACE]
    ORDER BY [Race_Name];
END;
GO