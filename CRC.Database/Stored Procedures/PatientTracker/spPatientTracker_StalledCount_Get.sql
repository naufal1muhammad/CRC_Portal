CREATE PROCEDURE [dbo].[spPatientTracker_StalledCount_Get]
AS
BEGIN
    SET NOCOUNT ON;

    -- Stalled = patient has at least one PatientAppointment AND the latest
    -- one's status is not 'Scheduled'.
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
    SELECT COUNT(*) AS [StalledCount]
    FROM LatestAppt la
    INNER JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = la.[Patient_ID]
    WHERE la.rn = 1
      AND la.[PatientAppointment_Status] <> 'Scheduled';
END;
GO
