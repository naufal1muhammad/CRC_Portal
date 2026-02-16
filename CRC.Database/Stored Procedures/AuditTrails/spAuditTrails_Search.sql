CREATE PROCEDURE [dbo].[spAuditTrails_Search]
	@UserId     INT         = NULL,
    @FromDate   DATE        = NULL,
    @ToDate     DATE        = NULL,
    @Action     VARCHAR(20) = NULL,
    @Category   VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /*
        AuditTrail_EventUTC is stored in UTC.
        For this UI, we display and filter in Malaysian time (UTC+8).
        Malaysia has no DST, so DATEADD(HOUR, 8, ...) is sufficient.
    */

    SELECT
        ISNULL(a.User_Id, 0) AS User_ID,
        ISNULL(u.User_Name, '') AS User_Name,
        DATEADD(HOUR, 8, a.AuditTrail_EventUTC) AS AuditTrail_EventMYT,
        a.AuditTrail_Action,
        a.AuditTrail_Category,
        a.AuditTrail_Summary
    FROM dbo.AuditTrails a
    LEFT JOIN dbo.Users u
        ON u.User_ID = a.User_Id
    WHERE
        (@UserId IS NULL OR a.User_Id = @UserId)
        AND (@Action IS NULL OR a.AuditTrail_Action = @Action)
        AND (@Category IS NULL OR a.AuditTrail_Category = @Category)
        AND (
            @FromDate IS NULL
            OR DATEADD(HOUR, 8, a.AuditTrail_EventUTC) >= CAST(@FromDate AS DATETIME2(0))
        )
        AND (
            @ToDate IS NULL
            OR DATEADD(HOUR, 8, a.AuditTrail_EventUTC) < DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2(0)))
        )
    ORDER BY a.AuditTrail_EventUTC DESC;
END
GO