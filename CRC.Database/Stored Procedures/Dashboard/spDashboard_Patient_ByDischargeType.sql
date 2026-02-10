CREATE PROCEDURE [dbo].[spDashboard_Patient_ByDischargeType]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COALESCE(NULLIF(LTRIM(RTRIM(dt.DischargeType_Name)), ''), 'Unknown') AS DischargeType_Name,
        COUNT(*) AS PatientCount
    FROM dbo.PatientBasic pb
    LEFT JOIN dbo.LU_DISCHARGETYPE dt ON dt.DischargeType_ID = pb.DischargeType_ID
    WHERE pb.DischargeType_ID IS NOT NULL
    GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(dt.DischargeType_Name)), ''), 'Unknown')
    ORDER BY PatientCount DESC;
END;
GO