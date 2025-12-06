CREATE PROCEDURE [dbo].[spLU_Religion_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Religion_ID], [Religion_Name]
    FROM [dbo].[LU_RELIGION]
    ORDER BY [Religion_Name];
END;
GO