CREATE PROCEDURE [dbo].[spPatientBasic_ListDischarged]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [Branch_Name],
        [Patient_AdmittedOn],
        [DischargeType_Name],
        [Patient_DischargeDate]
    FROM [dbo].[PatientBasic]
    WHERE [DischargeType_Name] IS NOT NULL   -- Discharged
    ORDER BY [Patient_DischargeDate] DESC, [Patient_ID] DESC;
END;
GO