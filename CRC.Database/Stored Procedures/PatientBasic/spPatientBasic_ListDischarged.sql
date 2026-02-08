CREATE PROCEDURE [dbo].[spPatientBasic_ListDischarged]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH FirstAppt AS
    (
        SELECT pa.Patient_ID,
               MIN(pa.PatientAppointment_Date) AS FirstAppointmentDate
        FROM dbo.PatientAppointment pa
        GROUP BY pa.Patient_ID
    )
    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Email],
        pb.[Patient_Phone],

        br.[Branch_Name],
        fa.FirstAppointmentDate AS [Patient_AdmittedOn],

        dt.[DischargeType_Name],
        pb.[Patient_DischargeDate]
    FROM dbo.PatientBasic pb
    LEFT JOIN FirstAppt fa            ON fa.Patient_ID = pb.Patient_ID
    OUTER APPLY
    (
        SELECT TOP 1 pa.Branch_ID
        FROM dbo.PatientAppointment pa
        WHERE pa.Patient_ID = pb.Patient_ID
        ORDER BY pa.PatientAppointment_Date DESC, pa.PatientAppointment_ID DESC
    ) lastAppt
    LEFT JOIN dbo.Branch br           ON br.Branch_ID = lastAppt.Branch_ID
    LEFT JOIN dbo.LU_DISCHARGETYPE dt ON dt.DischargeType_ID = pb.DischargeType_ID
    WHERE pb.[DischargeType_ID] IS NOT NULL  -- Discharged
    ORDER BY pb.[Patient_DischargeDate] DESC, pb.[Patient_ID] DESC;
END;
GO