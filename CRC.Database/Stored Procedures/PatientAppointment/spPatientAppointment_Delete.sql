CREATE PROCEDURE [dbo].[spPatientAppointment_Delete]
(
    @PatientAppointment_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[PatientAppointment]
    WHERE [PatientAppointment_ID] = @PatientAppointment_ID;
END;
GO