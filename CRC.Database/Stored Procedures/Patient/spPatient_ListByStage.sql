CREATE PROCEDURE [dbo].[spPatient_ListByStage]
    @Stage VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Patient_ID],
        [Patient_Name],
        [Patient_NRIC],
        [Patient_Phone],
        [Patient_Email],
        [Branch_ID],
        [Branch_Name],
        [Patient_Stage],
        [Patient_Remarks],
        [Appointment_Date]
    FROM [dbo].[Patient]
    WHERE [Patient_Stage] = @Stage
    ORDER BY [Patient_Name];
END;