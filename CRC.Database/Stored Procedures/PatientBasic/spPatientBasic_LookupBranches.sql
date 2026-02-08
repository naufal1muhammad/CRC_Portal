CREATE PROCEDURE [dbo].[spPatientBasic_LookupBranches]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        b.[Branch_Name]
    FROM dbo.Branch b
    WHERE b.[Branch_Status] = 1
      AND ISNULL(b.[Branch_Name], '') <> ''
    ORDER BY b.[Branch_Name];
END;
GO