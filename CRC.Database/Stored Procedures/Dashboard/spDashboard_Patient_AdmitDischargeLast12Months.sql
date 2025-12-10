CREATE PROCEDURE [dbo].[spDashboard_Patient_AdmitDischargeLast12Months]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentMonthStart DATE =
        DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    ;WITH MonthSequence AS
    (
        SELECT 0 AS Offset
        UNION ALL
        SELECT Offset + 1
        FROM MonthSequence
        WHERE Offset < 11   -- 0..11 = 12 months
    ),
    Months AS
    (
        SELECT DATEADD(MONTH, -Offset, @CurrentMonthStart) AS MonthStart
        FROM MonthSequence
    )
    SELECT
        YEAR(MonthStart)                         AS [Year],
        MONTH(MonthStart)                        AS [Month],
        FORMAT(MonthStart, 'MMM yyyy')           AS MonthLabel,
        -- Patients admitted in that month
        (
            SELECT COUNT(*)
            FROM [dbo].[PatientBasic] pb
            WHERE pb.Patient_AdmittedOn >= MonthStart
              AND pb.Patient_AdmittedOn < DATEADD(MONTH, 1, MonthStart)
        ) AS AdmittedCount,
        -- Patients discharged in that month
        (
            SELECT COUNT(*)
            FROM [dbo].[PatientBasic] pb
            WHERE pb.Patient_DischargeDate IS NOT NULL
              AND pb.Patient_DischargeDate >= MonthStart
              AND pb.Patient_DischargeDate < DATEADD(MONTH, 1, MonthStart)
        ) AS DischargedCount
    FROM Months
    ORDER BY [Year], [Month]
    OPTION (MAXRECURSION 12);
END;
GO