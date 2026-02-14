CREATE PROCEDURE [dbo].[spPatientAppointment_Delete]
(
    @PatientAppointment_ID INT,
    @User_ID               INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Capture details for audit (best effort)
    DECLARE
        @Patient_ID VARCHAR(100),
        @PatientAppointment_Date DATE,
        @Staff_ID VARCHAR(100),
        @PatientAppointment_StartTime TIME(0),
        @PatientAppointment_EndTime TIME(0),
        @PjAppType_ID VARCHAR(100),
        @Branch_ID VARCHAR(100),
        @PatientAppointment_Status VARCHAR(100);

    SELECT
        @Patient_ID = [Patient_ID],
        @PatientAppointment_Date = [PatientAppointment_Date],
        @Staff_ID = [Staff_ID],
        @PatientAppointment_StartTime = [PatientAppointment_StartTime],
        @PatientAppointment_EndTime = [PatientAppointment_EndTime],
        @PjAppType_ID = [PjAppType_ID],
        @Branch_ID = [Branch_ID],
        @PatientAppointment_Status = [PatientAppointment_Status]
    FROM [dbo].[PatientAppointment]
    WHERE [PatientAppointment_ID] = @PatientAppointment_ID;

    -- Release any booked slots first (FK safety)
    UPDATE dbo.StaffSlots
    SET PatientAppointment_ID = NULL
    WHERE PatientAppointment_ID = @PatientAppointment_ID;

    DELETE FROM [dbo].[PatientAppointment]
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
            'DELETE',
            'PatientAppointment',
            CONCAT(
                'Deleted Appointment: PatientAppointment_ID=', @PatientAppointment_ID,
                '; Patient_ID=', ISNULL(@Patient_ID, ''),
                '; Date=', ISNULL(CONVERT(VARCHAR(10), @PatientAppointment_Date, 23), ''),
                '; Time=', ISNULL(CONVERT(VARCHAR(8), @PatientAppointment_StartTime), ''), '-', ISNULL(CONVERT(VARCHAR(8), @PatientAppointment_EndTime), ''),
                '; Staff_ID=', ISNULL(@Staff_ID, ''),
                '; Branch_ID=', ISNULL(@Branch_ID, ''),
                '; Status=', ISNULL(@PatientAppointment_Status, ''),
                '; Type=', ISNULL(@PjAppType_ID, '')
            )
        );
    END
END;
GO