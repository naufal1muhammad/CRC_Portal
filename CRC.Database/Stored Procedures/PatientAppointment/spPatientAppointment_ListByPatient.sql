CREATE PROCEDURE [dbo].[spPatientAppointment_ListByPatient]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [PatientAppointment_ID],
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [PjAppType_Name],
        [PatientAppointment_Date],
        [PatientAppointment_Status]
    FROM [dbo].[PatientAppointment]
    WHERE [Patient_ID] = @Patient_ID
    ORDER BY [PatientAppointment_Date] DESC, [PatientAppointment_ID] DESC;
END;
GO