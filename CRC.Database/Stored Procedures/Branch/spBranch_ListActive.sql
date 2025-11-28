CREATE PROCEDURE [dbo].[spBranch_ListActive]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Branch_ID], [Branch_Name], [Branch_State]
    FROM [dbo].[Branch]
    WHERE [Branch_Status] = 1
    ORDER BY [Branch_Name];
END;