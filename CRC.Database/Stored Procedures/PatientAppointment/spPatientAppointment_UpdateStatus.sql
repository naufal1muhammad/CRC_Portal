CREATE PROCEDURE [dbo].[spPatientAppointment_UpdateStatus]
(
    @PatientAppointment_ID     INT,
    @PatientAppointment_Status VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PatientAppointment
    SET PatientAppointment_Status = @PatientAppointment_Status
    WHERE PatientAppointment_ID = @PatientAppointment_ID;

    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR('Appointment not found.', 16, 1);
        RETURN;
    END
END
GO