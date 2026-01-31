CREATE PROCEDURE [dbo].[spStaffSlots_List]
	@Staff_ID   VARCHAR(100),
    @FromDate   DATE = NULL,
    @ToDate     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        StaffSlot_ID,
        SlotDate,
        CONVERT(VARCHAR(5), SlotStartTime, 108) AS SlotStartTime,
        CONVERT(VARCHAR(5), SlotEndTime, 108)   AS SlotEndTime,
        PatientAppointment_ID
    FROM dbo.StaffSlots
    WHERE Staff_ID = @Staff_ID
      AND (@FromDate IS NULL OR SlotDate >= @FromDate)
      AND (@ToDate   IS NULL OR SlotDate <= @ToDate)
    ORDER BY SlotDate, SlotStartTime; -- keeps same ordering behavior
END
GO
