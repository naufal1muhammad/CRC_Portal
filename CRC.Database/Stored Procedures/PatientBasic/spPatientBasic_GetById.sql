CREATE PROCEDURE [dbo].[spPatientBasic_GetById]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Email],
        pb.[Patient_Phone],
        pb.[Patient_NRIC],

        pb.[Patient_BirthDate],
        pb.[Patient_Age],
        pb.[Race_ID],
        r.[Race_Name],
        pb.[Source_ID],
        s.[Source_Name],
        pb.[Patient_Gender],
        pb.[Religion_ID],
        rel.[Religion_Name],
        pb.[MaritalStatus_ID],
        ms.[MaritalStatus_Name],
        pb.[Occupation_ID],
        occ.[Occupation_Name],

        pb.[Patient_ResState],
        pb.[Patient_ResCity],
        pb.[Patient_ResPostcode],
        pb.[Patient_AddLine1],
        pb.[Patient_AddLine2],

        pb.[Patient_EmergencyName],
        pb.[Patient_EmergencyRelationship],
        pb.[Patient_EmergencyNumber],

        pb.[Patient_iFOBTStatus],
        pb.[Patient_iFOBTCompletionDate],
        pb.[Patient_iFOBTResults],

        pb.[DischargeType_ID],
        dt.[DischargeType_Name],
        pb.[Patient_DischargeDate],
        pb.[Patient_DischargeRemarks]
    FROM dbo.PatientBasic pb
    LEFT JOIN dbo.LU_RACE r              ON r.Race_ID = pb.Race_ID
    LEFT JOIN dbo.LU_SOURCE s            ON s.Source_ID = pb.Source_ID
    LEFT JOIN dbo.LU_RELIGION rel        ON rel.Religion_ID = pb.Religion_ID
    LEFT JOIN dbo.LU_MARITALSTATUS ms    ON ms.MaritalStatus_ID = pb.MaritalStatus_ID
    LEFT JOIN dbo.LU_OCCUPATION occ      ON occ.Occupation_ID = pb.Occupation_ID
    LEFT JOIN dbo.LU_DISCHARGETYPE dt    ON dt.DischargeType_ID = pb.DischargeType_ID
    WHERE pb.[Patient_ID] = @Patient_ID;
END;
GO