CREATE PROCEDURE [dbo].[spPatientAppointment_Delete]
(
    @PatientAppointment_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Release any booked slots first (FK safety)
    UPDATE dbo.StaffSlots
    SET PatientAppointment_ID = NULL
    WHERE PatientAppointment_ID = @PatientAppointment_ID;

    DELETE FROM [dbo].[PatientAppointment]
    WHERE [PatientAppointment_ID] = @PatientAppointment_ID;
END;
GO