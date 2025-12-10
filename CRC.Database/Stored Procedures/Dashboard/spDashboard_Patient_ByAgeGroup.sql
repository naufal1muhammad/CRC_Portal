CREATE PROCEDURE [dbo].[spDashboard_Patient_ByAgeGroup]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH AgeBuckets AS
    (
        SELECT
            CASE 
                WHEN Patient_Age IS NULL        THEN 'Unknown'
                WHEN Patient_Age <= 20          THEN '20 and below'
                WHEN Patient_Age BETWEEN 21 AND 40 THEN '21-40'
                WHEN Patient_Age BETWEEN 41 AND 60 THEN '41-60'
                WHEN Patient_Age BETWEEN 61 AND 80 THEN '61-80'
                ELSE '81 and above'
            END AS AgeGroup
        FROM [dbo].[PatientBasic]
    )
    SELECT
        AgeGroup,
        COUNT(*) AS PatientCount
    FROM AgeBuckets
    GROUP BY AgeGroup
    ORDER BY
        CASE AgeGroup
            WHEN '20 and below' THEN 1
            WHEN '21-40'        THEN 2
            WHEN '41-60'        THEN 3
            WHEN '61-80'        THEN 4
            WHEN '81 and above' THEN 5
            ELSE 6  -- Unknown
        END;
END;
GO