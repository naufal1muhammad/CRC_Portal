CREATE PROCEDURE [dbo].[spUsers_ValidateLogin]
    @Username VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [User_ID],
        [User_Name],
        [Username],
        [User_Email],
        [Password_Hash]        AS PasswordHash,
        [User_Type],
        [Staff_ID]             AS StaffId,
        [Created_At],
        [Last_Login],
        [Failed_Login_Count]   AS FailedLoginCount,
        [Last_Failed_Login_At] AS LastFailedLoginAt,
        [Lockout_End_Utc]      AS LockoutEndUtc
    FROM [dbo].[Users]
    WHERE [Username] = @Username;
END;
GO
