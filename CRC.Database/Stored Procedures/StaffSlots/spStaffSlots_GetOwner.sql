CREATE PROCEDURE [dbo].[spStaffSlots_GetOwner]
    @StaffSlot_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Returns the owning Staff_ID for a slot so callers can enforce ownership
    -- checks BEFORE invoking spStaffSlots_Delete. Returns an empty result set
    -- when the slot does not exist.
    SELECT TOP (1) [Staff_ID]
    FROM [dbo].[StaffSlots]
    WHERE [StaffSlot_ID] = @StaffSlot_ID;
END
GO
