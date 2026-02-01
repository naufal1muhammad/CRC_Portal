CREATE PROCEDURE [dbo].[spPatientAppointment_Update]
(
    @PatientAppointment_ID INT,
    @PatientAppointment_Date DATE,
    @Staff_ID VARCHAR(100),
    @PatientAppointment_StartTime TIME(0),
    @PatientAppointment_EndTime TIME(0),
    @PjAppType_ID VARCHAR(100),
    @Branch_ID VARCHAR(100),
    @PatientAppointment_Status VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[PatientAppointment]
    SET
        [PatientAppointment_Date] = @PatientAppointment_Date,
        [Staff_ID] = @Staff_ID,
        [PatientAppointment_StartTime] = @PatientAppointment_StartTime,
        [PatientAppointment_EndTime] = @PatientAppointment_EndTime,
        [PjAppType_ID] = @PjAppType_ID,
        [Branch_ID] = @Branch_ID,
        [PatientAppointment_Status] = @PatientAppointment_Status
    WHERE [PatientAppointment_ID] = @PatientAppointment_ID;
END;
GO