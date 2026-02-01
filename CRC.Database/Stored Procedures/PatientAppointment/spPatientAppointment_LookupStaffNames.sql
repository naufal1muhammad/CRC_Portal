CREATE PROCEDURE [dbo].[spPatientAppointment_LookupStaffNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        s.[Staff_Name]
    FROM [dbo].[PatientAppointment] pa
    INNER JOIN [dbo].[Staff] s
        ON pa.[Staff_ID] = s.[Staff_ID]
    WHERE ISNULL(s.[Staff_Name], '') <> ''
    ORDER BY s.[Staff_Name];
END;
GO