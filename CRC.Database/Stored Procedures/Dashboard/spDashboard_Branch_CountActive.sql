CREATE PROCEDURE [dbo].[spDashboard_Branch_CountActive]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS ActiveBranchCount
    FROM [dbo].[Branch]
    WHERE ISNULL(Branch_Status, 0) = 1;
END;
GO