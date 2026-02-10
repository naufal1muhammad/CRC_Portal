CREATE PROCEDURE [dbo].[spPatientAppointment_LookupBranches]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        b.[Branch_Name]
    FROM dbo.PatientAppointment pa
    INNER JOIN dbo.Branch b
        ON pa.[Branch_ID] = b.[Branch_ID]
    WHERE b.[Branch_Status] = 1
      AND ISNULL(b.[Branch_Name], '') <> ''
    ORDER BY b.[Branch_Name];
END;
GO
