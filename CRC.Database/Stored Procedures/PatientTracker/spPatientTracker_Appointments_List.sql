CREATE PROCEDURE [dbo].[spPatientTracker_Appointments_List]
AS
BEGIN
    SET NOCOUNT ON;

    -- For each (Patient_ID, PjAppType_ID), return only the latest appointment
    -- ordered by date, start time, then identity id (newest wins).
    ;WITH Ranked AS
    (
        SELECT
            pa.[Patient_ID],
            pa.[PjAppType_ID],
            pa.[PatientAppointment_Status],
            pa.[PatientAppointment_Date],
            ROW_NUMBER() OVER
            (
                PARTITION BY pa.[Patient_ID], pa.[PjAppType_ID]
                ORDER BY pa.[PatientAppointment_Date] DESC,
                         pa.[PatientAppointment_StartTime] DESC,
                         pa.[PatientAppointment_ID] DESC
            ) AS rn
        FROM [dbo].[PatientAppointment] pa
    )
    SELECT
        r.[Patient_ID],
        r.[PjAppType_ID],
        r.[PatientAppointment_Status],
        r.[PatientAppointment_Date]
    FROM Ranked r
    WHERE r.rn = 1;
END;
GO
