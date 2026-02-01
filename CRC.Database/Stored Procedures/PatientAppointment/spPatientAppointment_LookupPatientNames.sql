CREATE PROCEDURE [dbo].[spPatientAppointment_LookupPatientNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        pb.[Patient_Name]
    FROM [dbo].[PatientAppointment] pa
    INNER JOIN [dbo].[PatientBasic] pb
        ON pa.[Patient_ID] = pb.[Patient_ID]
    WHERE ISNULL(pb.[Patient_Name], '') <> ''
    ORDER BY pb.[Patient_Name];
END;
GO