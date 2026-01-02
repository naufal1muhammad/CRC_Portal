CREATE PROCEDURE [dbo].[spPatientAppointment_LookupStaffNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        [Staff_Name]
    FROM [dbo].[PatientAppointment]
    WHERE ISNULL([Staff_Name], '') <> ''
    ORDER BY [Staff_Name];
END;
GO