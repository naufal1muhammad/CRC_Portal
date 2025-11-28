CREATE PROCEDURE [dbo].[spDashboard_PatientStageCountsByState]
    @Branch_State VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Patient_Stage],
        COUNT(*) AS [PatientCount]
    FROM [dbo].[Patient]
    WHERE (@Branch_State IS NULL OR @Branch_State = '' OR [Branch_State] = @Branch_State)
    GROUP BY [Patient_Stage];
END;