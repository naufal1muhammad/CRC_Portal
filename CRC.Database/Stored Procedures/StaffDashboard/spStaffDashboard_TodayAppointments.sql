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
        pb.[Patient_Name] AS [Patient_Name],
        ap.[PjAppType_Name] AS [PjAppType_Name],
        pa.[PatientAppointment_Status],
        br.[Branch_Name] AS [Branch_Name],
        pa.[PatientAppointment_Date],
        pa.[PatientAppointment_StartTime],
        pa.[PatientAppointment_EndTime]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pa.[Patient_ID]
    LEFT JOIN [dbo].[LU_PJ_APP_TYPE] ap
        ON ap.[PjAppType_ID] = pa.[PjAppType_ID]
    LEFT JOIN [dbo].[Branch] br
        ON br.[Branch_ID] = pa.[Branch_ID]
    WHERE
        pa.[Staff_ID] = @Staff_ID
        AND pa.[PatientAppointment_Date] = @ForDate
    ORDER BY
        pa.[PatientAppointment_Date] ASC,
        pa.[PatientAppointment_StartTime] ASC,
        pa.[PatientAppointment_ID] ASC;
END
GO