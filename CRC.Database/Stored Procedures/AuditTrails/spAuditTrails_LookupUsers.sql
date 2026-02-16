CREATE PROCEDURE [dbo].[spAuditTrails_LookupUsers]
	AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        a.User_Id AS User_ID,
        u.User_Name
    FROM dbo.AuditTrails a
    INNER JOIN dbo.Users u
        ON u.User_ID = a.User_Id
    WHERE a.User_Id IS NOT NULL
    ORDER BY u.User_Name;
END
GO