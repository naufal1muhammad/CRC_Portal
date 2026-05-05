CREATE PROCEDURE [dbo].[spPatientTracker_Patients_List]
AS
BEGIN
    SET NOCOUNT ON;

    -- Latest PatientAppointment per Patient (by Date, StartTime, ID).
    -- A patient is considered "Stalled" when they have at least one
    -- appointment record AND the latest record's status is not 'Scheduled'.
    ;WITH LatestAppt AS
    (
        SELECT
            pa.[Patient_ID],
            pa.[PatientAppointment_Status],
            ROW_NUMBER() OVER
            (
                PARTITION BY pa.[Patient_ID]
                ORDER BY pa.[PatientAppointment_Date] DESC,
                         pa.[PatientAppointment_StartTime] DESC,
                         pa.[PatientAppointment_ID] DESC
            ) AS rn
        FROM [dbo].[PatientAppointment] pa
    )
    SELECT
        pb.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_NRIC],
        pb.[Patient_Phone],
        pb.[Patient_Age],
        pb.[Patient_Gender],
        pb.[Patient_DischargeDate],
        CAST(
            CASE
                WHEN la.[Patient_ID] IS NULL THEN 0
                WHEN la.[PatientAppointment_Status] = 'Scheduled' THEN 0
                ELSE 1
            END AS BIT
        ) AS [IsStalled]
    FROM [dbo].[PatientBasic] pb
    LEFT JOIN LatestAppt la
        ON la.[Patient_ID] = pb.[Patient_ID]
       AND la.rn = 1
    ORDER BY pb.[Patient_Name] ASC, pb.[Patient_ID] ASC;
END;
GO
