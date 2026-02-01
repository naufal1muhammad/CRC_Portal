CREATE PROCEDURE [dbo].[spStaffDashboard_AppointmentsByRange]
(
    @Staff_ID   VARCHAR(100),
    @StartDate  DATETIME,
    @EndDate    DATETIME
)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      Updated for new dbo.PatientAppointment schema (now stores IDs + Date/Time, not denormalized names).
      Keep the same output column names to avoid breaking existing Staff Dashboard UI.

      Note:
        dbo.PatientAppointment.PatientAppointment_Date is DATE, while this sproc accepts DATETIME.
        We preserve the original half-open interval logic: [StartDate, EndDate).
    */

    DECLARE @StartDateOnly DATE = CAST(@StartDate AS DATE);
    DECLARE @EndDateOnly   DATE = CAST(@EndDate   AS DATE);

    SELECT
        pa.[PatientAppointment_ID],
        pa.[Patient_ID],
        pb.[Patient_Name] AS [Patient_Name],
        ap.[PjAppType_Name] AS [PjAppType_Name],
        pa.[PatientAppointment_Status],
        pa.[Staff_ID],
        s.[Staff_Name] AS [Staff_Name],
        COALESCE(br.[Branch_Name], pb.[Branch_Name]) AS [Branch_Name],
        pa.[PatientAppointment_Date]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pb.[Patient_ID] = pa.[Patient_ID]
    LEFT JOIN [dbo].[Staff] s
        ON s.[Staff_ID] = pa.[Staff_ID]
    LEFT JOIN [dbo].[LU_PJ_APP_TYPE] ap
        ON ap.[PjAppType_ID] = pa.[PjAppType_ID]
    LEFT JOIN [dbo].[Branch] br
        ON br.[Branch_ID] = pa.[Branch_ID]
    WHERE
        pa.[Staff_ID] = @Staff_ID
        AND pa.[PatientAppointment_Date] >= @StartDateOnly
        AND pa.[PatientAppointment_Date] <  @EndDateOnly
    ORDER BY
        pa.[PatientAppointment_Date] ASC,
        pa.[PatientAppointment_StartTime] ASC,
        pa.[PatientAppointment_ID] ASC;
END
GO