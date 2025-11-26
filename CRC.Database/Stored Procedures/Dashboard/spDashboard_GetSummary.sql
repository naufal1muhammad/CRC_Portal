CREATE PROCEDURE [dbo].[spDashboard_GetSummary]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM [dbo].[Branch] WHERE [Branch_Status] = 1) AS ActiveBranchesCount,
        (SELECT COUNT(*) FROM [dbo].[Patient] WHERE [Patient_Stage] = 'T2') AS T2Count,
        (SELECT COUNT(*) FROM [dbo].[Patient] WHERE [Patient_Stage] = 'T3') AS T3Count,
        (SELECT COUNT(*) FROM [dbo].[Patient] WHERE [Patient_Stage] = 'T4') AS T4Count,
        (SELECT COUNT(*) FROM [dbo].[Patient] WHERE [Patient_Stage] = 'T5') AS T5Count;
END;