CREATE PROCEDURE [dbo].[spStaffSlots_Delete]
    @StaffSlot_ID INT
AS
BEGIN
    SET NOCOUNT ON;

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

    IF (@@ROWCOUNT = 0)
    BEGIN
        -- Not found, or already taken (covered above)
        ;THROW 50003, 'Slot not found.', 1;
    END
END
GO