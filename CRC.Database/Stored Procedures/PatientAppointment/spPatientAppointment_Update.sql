CREATE PROCEDURE [dbo].[spPatientAppointment_Update]
    @PatientAppointment_ID     INT,
    @PjAppType_Name            VARCHAR(100),
    @PatientAppointment_Date   DATETIME,
    @PatientAppointment_Status VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[PatientAppointment]
    SET
        [PjAppType_Name]          = @PjAppType_Name,
        [PatientAppointment_Date]  = @PatientAppointment_Date,
        [PatientAppointment_Status]= @PatientAppointment_Status
    WHERE [PatientAppointment_ID] = @PatientAppointment_ID;
END;