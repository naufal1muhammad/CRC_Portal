CREATE PROCEDURE [dbo].[spStaffSlots_CreateRange]
    @Staff_ID   VARCHAR(100),
    @FromDate   DATE,
    @ToDate     DATE,
    @StartTime  TIME(0),
    @EndTime    TIME(0),
    @User_ID    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF (NULLIF(LTRIM(RTRIM(@Staff_ID)), '') IS NULL)
        THROW 50001, 'Staff ID is required.', 1;

    IF (@ToDate < @FromDate)
        THROW 50001, 'To Date must be on or after From Date.', 1;

    -- Max 31 days inclusive
    IF (DATEDIFF(DAY, @FromDate, @ToDate) > 30)
        THROW 50001, 'Date range cannot exceed 31 days.', 1;

    IF (@EndTime <= @StartTime)
        THROW 50001, 'End Time must be later than Start Time.', 1;

    -- Times must be on the hour
    IF (DATEPART(MINUTE, @StartTime) <> 0 OR DATEPART(MINUTE, @EndTime) <> 0)
        THROW 50001, 'Times must be on the hour.', 1;

    DECLARE @HoursPerDay INT = DATEDIFF(HOUR, @StartTime, @EndTime);

    IF (@HoursPerDay <= 0)
        THROW 50001, 'Invalid time range.', 1;

    DECLARE @Slots TABLE
    (
        Staff_ID      VARCHAR(100) NOT NULL,
        SlotDate      DATE NOT NULL,
        SlotStartTime TIME(0) NOT NULL,
        SlotEndTime   TIME(0) NOT NULL
    );

    ;WITH Days AS
    (
        SELECT TOP (DATEDIFF(DAY, @FromDate, @ToDate) + 1)
            DATEADD(DAY, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1, @FromDate) AS SlotDate
        FROM sys.all_objects
    ),
    Hours AS
    (
        SELECT TOP (@HoursPerDay)
            DATEADD(HOUR, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1, @StartTime) AS SlotStartTime
        FROM sys.all_objects
    )
    INSERT INTO @Slots (Staff_ID, SlotDate, SlotStartTime, SlotEndTime)
    SELECT
        @Staff_ID AS Staff_ID,
        d.SlotDate,
        h.SlotStartTime,
        DATEADD(HOUR, 1, h.SlotStartTime) AS SlotEndTime
    FROM Days d
    CROSS JOIN Hours h
    WHERE DATEADD(HOUR, 1, h.SlotStartTime) <= @EndTime;

    DECLARE @AttemptedCount INT = (SELECT COUNT(*) FROM @Slots);

    -- Use MERGE to avoid duplicate key errors and to count inserts
    DECLARE @MergeOutput TABLE([Action] NVARCHAR(10));

    MERGE [dbo].[StaffSlots] WITH (HOLDLOCK) AS T
    USING @Slots AS S
        ON  T.[Staff_ID] = S.[Staff_ID]
        AND T.[SlotDate] = S.[SlotDate]
        AND T.[SlotStartTime] = S.[SlotStartTime]
    WHEN NOT MATCHED THEN
        INSERT ([Staff_ID], [SlotDate], [SlotStartTime], [SlotEndTime], [PatientAppointment_ID])
        VALUES (S.[Staff_ID], S.[SlotDate], S.[SlotStartTime], S.[SlotEndTime], NULL)
    OUTPUT $action INTO @MergeOutput;

    DECLARE @CreatedCount INT = (SELECT COUNT(*) FROM @MergeOutput WHERE [Action] = 'INSERT');
    DECLARE @SkippedExistingCount INT = @AttemptedCount - @CreatedCount;

    -- -----------------------------
    -- Audit trail
    -- -----------------------------
    INSERT INTO [dbo].[AuditTrails]
    (
        [User_Id],
        [AuditTrail_Action],
        [AuditTrail_Category],
        [AuditTrail_Summary]
    )
    VALUES
    (
        ISNULL(@User_ID, 0),
        'INSERT',
        'StaffSlots',
        CONCAT(
            'Created staff slots range: Staff_ID=', @Staff_ID,
            '; FromDate=', CONVERT(VARCHAR(10), @FromDate, 120),
            '; ToDate=', CONVERT(VARCHAR(10), @ToDate, 120),
            '; StartTime=', CONVERT(VARCHAR(8), @StartTime, 108),
            '; EndTime=', CONVERT(VARCHAR(8), @EndTime, 108),
            '; CreatedCount=', @CreatedCount,
            '; SkippedExistingCount=', @SkippedExistingCount
        )
    );

    SELECT
        @CreatedCount AS CreatedCount,
        @SkippedExistingCount AS SkippedExistingCount;
END
GO
