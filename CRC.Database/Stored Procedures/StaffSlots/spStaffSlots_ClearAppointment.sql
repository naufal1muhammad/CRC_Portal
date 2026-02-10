CREATE PROCEDURE [dbo].[spStaffSlots_ClearAppointment]
    @ApptId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[StaffSlots]
    SET [PatientAppointment_ID] = NULL
    WHERE [PatientAppointment_ID] = @ApptId;
END
GO
