CREATE PROCEDURE dbo.spUsers_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        User_ID,
        User_Name,
        Username,
        User_Email,
        User_Type,
        Staff_ID,
        Created_At,
        Last_Login,
        Failed_Login_Count   AS FailedLoginCount,
        Last_Failed_Login_At AS LastFailedLoginAt,
        Lockout_End_Utc      AS LockoutEndUtc
    FROM dbo.Users
    ORDER BY User_ID DESC;
END
GO
