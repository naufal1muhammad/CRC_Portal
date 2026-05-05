CREATE PROCEDURE [dbo].[spPatientTracker_AppointmentTypes_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [PjAppType_ID],
        [PjAppType_Name]
    FROM [dbo].[LU_PJ_APP_TYPE]
    ORDER BY [PjAppType_ID] ASC;
END;
GO
