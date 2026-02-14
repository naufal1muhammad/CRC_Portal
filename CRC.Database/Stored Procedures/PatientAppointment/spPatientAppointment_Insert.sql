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
    @NewPatientAppointment_ID INT OUTPUT,
    @User_ID INT = NULL
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

    DECLARE @InsertedAppointmentId INT = CONVERT(INT, SCOPE_IDENTITY());
    SET @NewPatientAppointment_ID = @InsertedAppointmentId;

    -- -----------------------------
    -- Audit trail
    -- -----------------------------
    INSERT INTO [dbo].[AuditTrails]
    (
        [User_Id],
        [AuditTrail_Action],
        [AuditTrail_Category],
        [AuditTrail_Summary]
    )
    VALUES
    (
        ISNULL(@User_ID, 0),
        'INSERT',
        'PatientAppointment',
        CONCAT(
            'Created Appointment: PatientAppointment_ID=', @InsertedAppointmentId,
            '; Patient_ID=', @Patient_ID,
            '; Date=', CONVERT(VARCHAR(10), @PatientAppointment_Date, 23),
            '; Time=', CONVERT(VARCHAR(8), @PatientAppointment_StartTime), '-', CONVERT(VARCHAR(8), @PatientAppointment_EndTime),
            '; Staff_ID=', @Staff_ID,
            '; Branch_ID=', @Branch_ID,
            '; Status=', @PatientAppointment_Status,
            '; Type=', @PjAppType_ID
        )
    );
END;
GO