CREATE PROCEDURE [dbo].[spDashboard_Patient_CountDischargedByBranch]
(
    @Branch_Name VARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS DischargedPatientCount
    FROM [dbo].[PatientBasic]
    WHERE DischargeType_Name IS NOT NULL
      AND Patient_DischargeDate IS NOT NULL
      AND (
              @Branch_Name IS NULL
           OR @Branch_Name = ''
           OR Branch_Name = @Branch_Name
          );
END;
GO