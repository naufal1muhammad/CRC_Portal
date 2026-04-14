CREATE PROCEDURE [dbo].[spPatientBasic_ListDischarged]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_DischargeDate],
        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTCompletionDate],
        pb.[Patient_iFOBTResults]
    FROM dbo.PatientBasic pb
    WHERE pb.[DischargeType_ID] IS NOT NULL  -- Discharged
    ORDER BY pb.[Patient_DischargeDate] DESC, pb.[Patient_ID] DESC;
END;
GO