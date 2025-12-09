CREATE PROCEDURE [dbo].[spPatientAppointment_Search]
(
    @PatientName       VARCHAR(100) = NULL,
    @StaffName         VARCHAR(100) = NULL,
    @Status            VARCHAR(100) = NULL,
    @FromDate          DATE        = NULL,
    @ToDate            DATE        = NULL,
    @PjAppTypeName     VARCHAR(100) = NULL,
    @BranchName        VARCHAR(100) = NULL
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
        -- Patient Name filter (exact match from dropdown)
        (
            @PatientName IS NULL
            OR LTRIM(RTRIM(@PatientName)) = ''
            OR pa.[Patient_Name] = @PatientName
        )
        -- Staff Name filter
        AND (
            @StaffName IS NULL
            OR LTRIM(RTRIM(@StaffName)) = ''
            OR pa.[Staff_Name] = @StaffName
        )
        -- Attendance Status filter
        AND (
            @Status IS NULL
            OR LTRIM(RTRIM(@Status)) = ''
            OR pa.[PatientAppointment_Status] = @Status
        )
        -- Appointment Type filter
        AND (
            @PjAppTypeName IS NULL
            OR LTRIM(RTRIM(@PjAppTypeName)) = ''
            OR pa.[PjAppType_Name] = @PjAppTypeName
        )
        -- Branch filter (coming from PatientBasic via join)
        AND (
            @BranchName IS NULL
            OR LTRIM(RTRIM(@BranchName)) = ''
            OR pb.[Branch_Name] = @BranchName
        )
        -- From Date filter (start of range, inclusive)
        AND (
            @FromDate IS NULL
            OR pa.[PatientAppointment_Date] >= @FromDate
        )
        -- To Date filter (end of range, inclusive)
        AND (
            @ToDate IS NULL
            OR pa.[PatientAppointment_Date] < DATEADD(DAY, 1, @ToDate)
        )
    ORDER BY
        pa.[PatientAppointment_Date] DESC,
        pa.[PatientAppointment_ID] DESC;
END;
GO