CREATE PROCEDURE [dbo].[spPatientDocumentSettings_GetByDischargeType]
(
    @DischargeType_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [DischargeType_ID],
        [DischargeType_Name],
        [PatientDocumentType_ID],
        [PatientDocumentType_Name]
    FROM [dbo].[PatientDocumentSettings]
    WHERE [DischargeType_ID] = @DischargeType_ID
    ORDER BY [PatientDocumentType_Name];
END;
GO