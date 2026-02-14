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
    @User_ID INT = NULL
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