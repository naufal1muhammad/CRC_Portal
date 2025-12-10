CREATE PROCEDURE [dbo].[spDashboard_Patient_ByRace]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CASE 
            WHEN ISNULL(Race_Name, '') = '' THEN 'Unknown'
            ELSE Race_Name
        END AS Race_Name,
        COUNT(*) AS PatientCount
    FROM [dbo].[PatientBasic]
    GROUP BY CASE 
                 WHEN ISNULL(Race_Name, '') = '' THEN 'Unknown'
                 ELSE Race_Name
             END
    ORDER BY PatientCount DESC;
END;
GO