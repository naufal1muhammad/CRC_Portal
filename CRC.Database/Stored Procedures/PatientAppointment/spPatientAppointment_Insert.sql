CREATE PROCEDURE [dbo].[spPatientAppointment_Insert]
(
    @Patient_ID VARCHAR(100),
    @PatientAppointment_Date DATE,
    @Staff_ID VARCHAR(100),
    @PatientAppointment_StartTime TIME(0),
    @PatientAppointment_EndTime TIME(0),
    @PjAppType_ID VARCHAR(100),
    @Branch_ID VARCHAR(100),
    @PatientAppointment_Status VARCHAR(100),
    @NewPatientAppointment_ID INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[PatientAppointment]
    (
        [Patient_ID],
        [PatientAppointment_Date],
        [Staff_ID],
        [PatientAppointment_StartTime],
        [PatientAppointment_EndTime],
        [PjAppType_ID],
        [Branch_ID],
        [PatientAppointment_Status]
    )
    VALUES
    (
        @Patient_ID,
        @PatientAppointment_Date,
        @Staff_ID,
        @PatientAppointment_StartTime,
        @PatientAppointment_EndTime,
        @PjAppType_ID,
        @Branch_ID,
        @PatientAppointment_Status
    );

    SET @NewPatientAppointment_ID = CONVERT(INT, SCOPE_IDENTITY());
END;
GO