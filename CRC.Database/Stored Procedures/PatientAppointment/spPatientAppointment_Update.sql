CREATE PROCEDURE [dbo].[spPatientAppointment_Update]
(
    @PatientAppointment_ID INT,
    @PatientAppointment_Date DATE,
    @Staff_ID VARCHAR(100),
    @PatientAppointment_StartTime TIME(0),
    @PatientAppointment_EndTime TIME(0),
    @PjAppType_ID VARCHAR(100),
    @Branch_ID VARCHAR(100),
    @PatientAppointment_Status VARCHAR(100),
    @User_ID INT = NULL,
    @Out_Patient_ID VARCHAR(100) = NULL OUTPUT,
    @Out_Staff_ID VARCHAR(100) = NULL OUTPUT,
    @Out_Date DATE = NULL OUTPUT,
    @Out_StartTime TIME(0) = NULL OUTPUT,
    @Out_EndTime TIME(0) = NULL OUTPUT,
    @Out_PjAppType_ID VARCHAR(100) = NULL OUTPUT,
    @Out_Branch_ID VARCHAR(100) = NULL OUTPUT,
    @Out_Status VARCHAR(100) = NULL OUTPUT
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

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    IF @RowsAffected > 0
    BEGIN
        -- Re-read persisted values so callers can audit DB state, not request payload
        SELECT
            @Out_Patient_ID    = [Patient_ID],
            @Out_Staff_ID      = [Staff_ID],
            @Out_Date          = [PatientAppointment_Date],
            @Out_StartTime     = [PatientAppointment_StartTime],
            @Out_EndTime       = [PatientAppointment_EndTime],
            @Out_PjAppType_ID  = [PjAppType_ID],
            @Out_Branch_ID     = [Branch_ID],
            @Out_Status        = [PatientAppointment_Status]
        FROM [dbo].[PatientAppointment]
        WHERE [PatientAppointment_ID] = @PatientAppointment_ID;

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
            'UPDATE',
            'PatientAppointment',
            CONCAT(
                'Updated Appointment: PatientAppointment_ID=', @PatientAppointment_ID,
                '; Date=', CONVERT(VARCHAR(10), @PatientAppointment_Date, 23),
                '; Time=', CONVERT(VARCHAR(8), @PatientAppointment_StartTime), '-', CONVERT(VARCHAR(8), @PatientAppointment_EndTime),
                '; Staff_ID=', @Staff_ID,
                '; Branch_ID=', @Branch_ID,
                '; Status=', @PatientAppointment_Status,
                '; Type=', @PjAppType_ID
            )
        );
    END
END;
GO