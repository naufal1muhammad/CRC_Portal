CREATE PROCEDURE [dbo].[spDashboard_Patient_ByDischargeType]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DischargeType_Name,
        COUNT(*) AS PatientCount
    FROM [dbo].[PatientBasic]
    WHERE DischargeType_Name IS NOT NULL
    GROUP BY DischargeType_Name
    ORDER BY PatientCount DESC;
END;
GO