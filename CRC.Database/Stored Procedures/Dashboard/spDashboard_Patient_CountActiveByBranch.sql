CREATE PROCEDURE [dbo].[spDashboard_Patient_CountActiveByBranch]
(
    @Branch_Name VARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS ActivePatientCount
    FROM [dbo].[PatientBasic]
    WHERE DischargeType_Name IS NULL
      AND Patient_DischargeDate IS NULL
      AND (
              @Branch_Name IS NULL
           OR @Branch_Name = ''
           OR Branch_Name = @Branch_Name
          );
END;
GO