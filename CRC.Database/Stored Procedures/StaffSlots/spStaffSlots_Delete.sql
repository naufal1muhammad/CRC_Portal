CREATE PROCEDURE [dbo].[spStaffSlots_Delete]
    @StaffSlot_ID INT,
    @User_ID      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Staff_ID VARCHAR(100);
    DECLARE @SlotDate DATE;
    DECLARE @SlotStartTime TIME(0);
    DECLARE @SlotEndTime TIME(0);

    SELECT
        @Staff_ID = [Staff_ID],
        @SlotDate = [SlotDate],
        @SlotStartTime = [SlotStartTime],
        @SlotEndTime = [SlotEndTime]
    FROM [dbo].[StaffSlots]
    WHERE [StaffSlot_ID] = @StaffSlot_ID;

    IF EXISTS (SELECT 1
               FROM [dbo].[StaffSlots]
               WHERE [StaffSlot_ID] = @StaffSlot_ID
                 AND [PatientAppointment_ID] IS NOT NULL)
    BEGIN
        ;THROW 50002, 'Cannot delete a slot that is already taken.', 1;
    END

    DELETE FROM [dbo].[StaffSlots]
    WHERE [StaffSlot_ID] = @StaffSlot_ID
      AND [PatientAppointment_ID] IS NULL;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    IF (@RowsAffected = 0)
    BEGIN
        -- Not found, or already taken (covered above)
        ;THROW 50003, 'Slot not found.', 1;
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
        'DELETE',
        'StaffSlots',
        CONCAT(
            'Deleted Staff Slot: StaffSlot_ID=', @StaffSlot_ID,
            '; Staff_ID=', ISNULL(@Staff_ID, ''),
            '; SlotDate=', ISNULL(CONVERT(VARCHAR(10), @SlotDate, 120), ''),
            '; SlotStartTime=', ISNULL(CONVERT(VARCHAR(8), @SlotStartTime, 108), ''),
            '; SlotEndTime=', ISNULL(CONVERT(VARCHAR(8), @SlotEndTime, 108), '')
        )
    );
END
GO
