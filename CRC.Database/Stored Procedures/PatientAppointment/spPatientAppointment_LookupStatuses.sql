CREATE PROCEDURE [dbo].[spPatientAppointment_LookupStatuses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        [PatientAppointment_Status]
    FROM [dbo].[PatientAppointment]
    WHERE ISNULL([PatientAppointment_Status], '') <> ''
    ORDER BY [PatientAppointment_Status];
END;
GO