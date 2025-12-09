CREATE PROCEDURE [dbo].[spPatientDocument_PatientNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        Patient_Name
    FROM [dbo].[PatientDocument]
    WHERE ISNULL(Patient_Name, '') <> ''
    ORDER BY Patient_Name;
END;
GO