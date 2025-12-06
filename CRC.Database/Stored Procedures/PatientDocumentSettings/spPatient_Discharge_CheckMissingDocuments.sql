CREATE PROCEDURE [dbo].[spPatient_Discharge_CheckMissingDocuments]
(
    @Patient_ID       VARCHAR(100),
    @DischargeType_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    /*
        Returns list of mandatory document types that are MISSING
        for this patient & discharge type.

        If result set is empty => all good; no missing docs.
    */

    SELECT
        s.[PatientDocumentType_ID],
        s.[PatientDocumentType_Name]
    FROM [dbo].[PatientDocumentSettings] s
    WHERE s.[DischargeType_ID] = @DischargeType_ID
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[PatientDocument] d
          WHERE d.[Patient_ID] = @Patient_ID
            AND ISNULL(d.[PatientDocumentType_ID], '') = s.[PatientDocumentType_ID]
      );
END;
GO