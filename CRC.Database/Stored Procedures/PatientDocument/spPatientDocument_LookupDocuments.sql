CREATE PROCEDURE [dbo].[spPatientDocument_LookupDocuments]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        PatientDocumentType_Name
    FROM [dbo].[PatientDocument]
    WHERE ISNULL(LTRIM(RTRIM(PatientDocumentType_Name)), '') <> ''
    ORDER BY PatientDocumentType_Name;
END;
GO
