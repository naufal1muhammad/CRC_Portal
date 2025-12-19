CREATE PROCEDURE [dbo].[spStaffDashboard_TodayAppointments]
(
    @Staff_ID  VARCHAR(100),
    @ForDate   DATE
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pa.[PatientAppointment_ID],
        pa.[Patient_ID],
        pa.[Patient_Name],
        pa.[Patient_Phone],
        pa.[Patient_Email],
        pa.[PjAppType_Name],
        pa.[PatientAppointment_Status],
        pa.[Staff_ID],
        pa.[Staff_Name],
        pb.[Branch_Name],
        pa.[PatientAppointment_Date]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pa.[Patient_ID]
    WHERE
        pa.[Staff_ID] = @Staff_ID
        AND pa.[PatientAppointment_Date] >= @ForDate
        AND pa.[PatientAppointment_Date] < DATEADD(DAY, 1, @ForDate)
    ORDER BY
        pa.[PatientAppointment_Date] ASC,
        pa.[PatientAppointment_ID] ASC;
END
GO