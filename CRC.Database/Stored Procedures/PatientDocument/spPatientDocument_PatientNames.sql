CREATE PROCEDURE [dbo].[spPatientDocument_PatientNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        pb.[Patient_Name]
    FROM [dbo].[PatientDocument] pd
    INNER JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pd.[Patient_ID]
    WHERE ISNULL(pb.[Patient_Name], '') <> ''
    ORDER BY pb.[Patient_Name];
END;
GO