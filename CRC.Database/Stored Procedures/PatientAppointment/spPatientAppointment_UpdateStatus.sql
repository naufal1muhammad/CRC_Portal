CREATE PROCEDURE [dbo].[spPatientAppointment_UpdateStatus]
(
    @PatientAppointment_ID     INT,
    @PatientAppointment_Status VARCHAR(100),
    @User_ID                  INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PatientAppointment
    SET PatientAppointment_Status = @PatientAppointment_Status
    WHERE PatientAppointment_ID = @PatientAppointment_ID;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    IF @RowsAffected = 0
    BEGIN
        RAISERROR('Appointment not found.', 16, 1);
        RETURN;
    END

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
            'Updated Appointment Status: PatientAppointment_ID=', @PatientAppointment_ID,
            '; Status=', @PatientAppointment_Status
        )
    );
END
GO