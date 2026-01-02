CREATE PROCEDURE [dbo].[spLU_PatientDocumentType_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [PatientDocumentType_ID], [PatientDocumentType_Name]
    FROM [dbo].[LU_PATDOCUMENTTYPE]
    ORDER BY [PatientDocumentType_Name];
END;
GO