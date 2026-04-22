CREATE PROCEDURE [dbo].[spStaff_GetPerformance]
    @Staff_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthStart DATETIME2(0) = DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1);
    DECLARE @NextMonthStart DATETIME2(0) = DATEADD(MONTH, 1, @MonthStart);

    -- Result set 1: Summary counts
    SELECT
        SUM(CASE
                WHEN UPPER([PjAppType_Name]) = 'COLONOSCOPY' THEN 1
                ELSE 0
            END) AS [TotalColonoscopy],
        SUM(CASE
                WHEN UPPER([PjAppType_Name]) = 'COLONOSCOPY'
                     AND [Created_At] >= @MonthStart
                     AND [Created_At] <  @NextMonthStart THEN 1
                ELSE 0
            END) AS [TotalColonoscopyThisMonth]
    FROM [dbo].[PatientJourney]
    WHERE [Staff_ID] = @Staff_ID;

    -- Result set 2: Total hours spent per appointment type (status = Attended)
    SELECT
        pa.[PjAppType_ID],
        lu.[PjAppType_Name],
        CAST(
            SUM(DATEDIFF(MINUTE, pa.[PatientAppointment_StartTime], pa.[PatientAppointment_EndTime]))
            / 60.0
            AS DECIMAL(10, 2)
        ) AS [TotalHours]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[LU_PJ_APP_TYPE] lu
        ON lu.[PjAppType_ID] = pa.[PjAppType_ID]
    WHERE pa.[Staff_ID] = @Staff_ID
      AND pa.[PatientAppointment_Status] = 'Attended'
      AND pa.[PatientAppointment_StartTime] IS NOT NULL
      AND pa.[PatientAppointment_EndTime]   IS NOT NULL
      AND pa.[PatientAppointment_EndTime]   > pa.[PatientAppointment_StartTime]
    GROUP BY pa.[PjAppType_ID], lu.[PjAppType_Name]
    ORDER BY lu.[PjAppType_Name];

    -- Result set 3: Complications (non-empty distinct values, count of colonoscopy rows)
    SELECT
        pc.[Complications] AS [Complication],
        COUNT(*)           AS [Total]
    FROM [dbo].[PatientColonoscopy] pc
    INNER JOIN [dbo].[PatientJourney] pj
        ON pj.[PatientJourney_ID] = pc.[PatientJourney_ID]
    WHERE pj.[Staff_ID] = @Staff_ID
      AND pc.[Complications] IS NOT NULL
      AND LTRIM(RTRIM(pc.[Complications])) <> ''
    GROUP BY pc.[Complications]
    ORDER BY pc.[Complications];

    -- Result set 4: Anomalies detected across all Findings_*Details JSON columns,
    -- deduplicated by Patient_ID per TypeOfAnomaly.
    ;WITH Findings AS (
        SELECT
            pj.[Patient_ID],
            JSON_VALUE(v.[FindingsJson], '$.TypeOfAnomaly') AS [TypeOfAnomaly]
        FROM [dbo].[PatientColonoscopy] pc
        INNER JOIN [dbo].[PatientJourney] pj
            ON pj.[PatientJourney_ID] = pc.[PatientJourney_ID]
        CROSS APPLY (VALUES
            (pc.[Findings_AnusDetails]),
            (pc.[Findings_RectumDetails]),
            (pc.[Findings_SigmoidColonDetails]),
            (pc.[Findings_DescendingColonDetails]),
            (pc.[Findings_SplenicFlexureDetails]),
            (pc.[Findings_TransverseColonDetails]),
            (pc.[Findings_HepaticFlexureDetails]),
            (pc.[Findings_AscendingColonDetails]),
            (pc.[Findings_CaecumDetails])
        ) v ([FindingsJson])
        WHERE pj.[Staff_ID] = @Staff_ID
          AND v.[FindingsJson] IS NOT NULL
          AND ISJSON(v.[FindingsJson]) = 1
    )
    SELECT
        [TypeOfAnomaly],
        COUNT(DISTINCT [Patient_ID]) AS [PatientCount]
    FROM Findings
    WHERE [TypeOfAnomaly] IS NOT NULL
      AND LTRIM(RTRIM([TypeOfAnomaly])) <> ''
    GROUP BY [TypeOfAnomaly]
    ORDER BY [TypeOfAnomaly];
END;
GO
