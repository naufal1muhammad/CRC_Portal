CREATE PROCEDURE [dbo].[spPatient_GetById]
    @Patient_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [Patient_ID],
        [Patient_Name],
        [Patient_NRIC],
        [Patient_Phone],
        [Patient_Email],
        [Branch_ID],
        [Branch_Name],
        [Branch_State],
        [Patient_Stage],
        [Patient_Remarks],
        [Appointment_Date]
    FROM [dbo].[Patient]
    WHERE [Patient_ID] = @Patient_ID;
END;