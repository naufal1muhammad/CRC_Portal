CREATE PROCEDURE [dbo].[spDashboard_Patient_ByRace]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COALESCE(NULLIF(LTRIM(RTRIM(r.Race_Name)), ''), 'Unknown') AS Race_Name,
        COUNT(*) AS PatientCount
    FROM dbo.PatientBasic pb
    LEFT JOIN dbo.LU_RACE r ON r.Race_ID = pb.Race_ID
    GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(r.Race_Name)), ''), 'Unknown')
    ORDER BY PatientCount DESC;
END;
GO