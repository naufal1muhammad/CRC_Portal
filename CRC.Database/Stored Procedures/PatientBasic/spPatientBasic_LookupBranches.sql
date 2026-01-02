CREATE PROCEDURE [dbo].[spPatientBasic_LookupBranches]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        [Branch_Name]
    FROM [dbo].[PatientBasic]
    WHERE ISNULL([Branch_Name], '') <> ''
    ORDER BY [Branch_Name];
END;
GO