CREATE PROCEDURE [dbo].[spPatientAppointment_Search]
(
    @PatientName   VARCHAR(100) = NULL,
    @StaffName     VARCHAR(100) = NULL,
    @Status        VARCHAR(100) = NULL,
    @FromDate      DATE         = NULL,
    @ToDate        DATE         = NULL,
    @PjAppTypeName VARCHAR(100) = NULL,
    @BranchName    VARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pa.[PatientAppointment_ID],
        pa.[Patient_ID],
        pb.[Patient_Name],
        pb.[Patient_Phone],
        pb.[Patient_Email],
        -- Prefer lookup name; fall back to stored value in PatientAppointment in case legacy rows store the name instead of the ID
        COALESCE(t.[PjAppType_Name], pa.[PjAppType_ID]) AS [PjAppType_Name],
        pa.[PatientAppointment_Status],
        s.[Staff_Name],
        b.[Branch_Name],
        -- Keep legacy column name used by existing controllers/JS (start datetime)
        DATEADD(SECOND, DATEDIFF(SECOND, 0, pa.[PatientAppointment_StartTime]),
            CAST(pa.[PatientAppointment_Date] AS DATETIME)) AS [PatientAppointment_Date]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[PatientBasic] pb
        ON pa.[Patient_ID] = pb.[Patient_ID]
    LEFT JOIN [dbo].[Staff] s
        ON pa.[Staff_ID] = s.[Staff_ID]
    LEFT JOIN [dbo].[LU_PJ_APP_TYPE] t
        ON pa.[PjAppType_ID] = t.[PjAppType_ID]
    LEFT JOIN [dbo].[Branch] b
        ON pa.[Branch_ID] = b.[Branch_ID]
    WHERE
        (@PatientName IS NULL OR pb.[Patient_Name] = @PatientName)
        AND (@StaffName IS NULL OR s.[Staff_Name] = @StaffName)
        AND (@Status IS NULL OR pa.[PatientAppointment_Status] = @Status)
        -- Appointment Type filter: support filtering by either type NAME (UI dropdown) or type ID (stored value)
        -- This also makes the search resilient if older rows stored the name in PatientAppointment.PjAppType_ID.
        AND (
            @PjAppTypeName IS NULL
            OR LTRIM(RTRIM(t.[PjAppType_Name])) = LTRIM(RTRIM(@PjAppTypeName))
            OR LTRIM(RTRIM(t.[PjAppType_ID])) = LTRIM(RTRIM(@PjAppTypeName))
            OR LTRIM(RTRIM(pa.[PjAppType_ID])) = LTRIM(RTRIM(@PjAppTypeName))
        )
        AND (@BranchName IS NULL OR b.[Branch_Name] = @BranchName)
        AND (@FromDate IS NULL OR pa.[PatientAppointment_Date] >= @FromDate)
        AND (@ToDate IS NULL OR pa.[PatientAppointment_Date] < DATEADD(DAY, 1, @ToDate))
    ORDER BY
        pa.[PatientAppointment_Date] DESC,
        pa.[PatientAppointment_StartTime] ASC,
        pa.[PatientAppointment_ID] DESC;
END;
GO