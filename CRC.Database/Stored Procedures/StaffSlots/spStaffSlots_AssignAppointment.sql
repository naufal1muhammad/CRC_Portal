CREATE PROCEDURE [dbo].[spStaffSlots_AssignAppointment]
    @ApptId INT,
    @StaffSlotIds VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF (NULLIF(LTRIM(RTRIM(@StaffSlotIds)), '') IS NULL)
        THROW 50001, 'At least one staff slot ID is required.', 1;

    ;WITH SlotIds AS
    (
        SELECT DISTINCT TRY_CAST(value AS INT) AS StaffSlot_ID
        FROM STRING_SPLIT(@StaffSlotIds, ',')
        WHERE TRY_CAST(value AS INT) IS NOT NULL
    )
    UPDATE SS
    SET SS.[PatientAppointment_ID] = @ApptId
    FROM [dbo].[StaffSlots] SS
    INNER JOIN SlotIds SI ON SI.StaffSlot_ID = SS.[StaffSlot_ID];
END
GO
