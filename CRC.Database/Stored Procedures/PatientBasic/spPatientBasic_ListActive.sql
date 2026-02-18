CREATE PROCEDURE [dbo].[spPatientBasic_ListActive]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTCompletionDate],
        pb.[Patient_iFOBTResults],
        pb.[Patient_IsApproved]
    FROM dbo.PatientBasic pb
    WHERE pb.[DischargeType_ID] IS NULL  -- Active = not discharged
    ORDER BY pb.[Patient_ID] DESC;
END;
GO