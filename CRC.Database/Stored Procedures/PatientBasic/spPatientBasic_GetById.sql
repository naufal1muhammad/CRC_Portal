CREATE PROCEDURE [dbo].[spPatientBasic_GetById]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH FirstAppt AS
    (
        SELECT pa.Patient_ID,
               MIN(pa.PatientAppointment_Date) AS FirstAppointmentDate
        FROM dbo.PatientAppointment pa
        WHERE pa.Patient_ID = @Patient_ID
        GROUP BY pa.Patient_ID
    )
    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Email],
        pb.[Patient_Phone],
        pb.[Patient_NRIC],

        fa.FirstAppointmentDate AS [Patient_AdmittedOn],

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

        -- Backward compatible computed address (old UI/staff view)
        (pb.[Patient_AddLine1]
            + CASE WHEN pb.[Patient_AddLine2] IS NOT NULL AND LTRIM(RTRIM(pb.[Patient_AddLine2])) <> '' THEN CHAR(10) + pb.[Patient_AddLine2] ELSE '' END
            + CASE WHEN ISNULL(pb.[Patient_ResPostcode], '') <> '' THEN CHAR(10) + pb.[Patient_ResPostcode] ELSE '' END
            + CASE WHEN ISNULL(pb.[Patient_ResCity], '') <> '' THEN ' ' + pb.[Patient_ResCity] ELSE '' END
            + CASE WHEN ISNULL(pb.[Patient_ResState], '') <> '' THEN ', ' + pb.[Patient_ResState] ELSE '' END
        ) AS [Patient_Address],

        pb.[Patient_EmergencyName],
        pb.[Patient_EmergencyRelationship],
        pb.[Patient_EmergencyNumber],

        -- Backward compatible derived branch name (from latest appointment)
        br.[Branch_Name],

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
    LEFT JOIN FirstAppt fa               ON fa.Patient_ID = pb.Patient_ID
    OUTER APPLY
    (
        SELECT TOP 1 pa.Branch_ID
        FROM dbo.PatientAppointment pa
        WHERE pa.Patient_ID = pb.Patient_ID
        ORDER BY pa.PatientAppointment_Date DESC, pa.PatientAppointment_ID DESC
    ) lastAppt
    LEFT JOIN dbo.Branch br ON br.Branch_ID = lastAppt.Branch_ID
    WHERE pb.[Patient_ID] = @Patient_ID;
END;
GO