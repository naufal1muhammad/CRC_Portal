CREATE PROCEDURE [dbo].[spPatientAppointment_LookupPatientNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        [Patient_Name]
    FROM [dbo].[PatientAppointment]
    WHERE ISNULL([Patient_Name], '') <> ''
    ORDER BY [Patient_Name];
END;
GO