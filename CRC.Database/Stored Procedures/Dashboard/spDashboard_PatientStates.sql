CREATE PROCEDURE [dbo].[spDashboard_PatientStates]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT [Branch_State]
    FROM [dbo].[Patient]
    WHERE [Branch_State] IS NOT NULL
      AND LTRIM(RTRIM([Branch_State])) <> ''
    ORDER BY [Branch_State];
END;