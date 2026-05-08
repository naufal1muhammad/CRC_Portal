CREATE PROCEDURE [dbo].[spUsers_ResetFailedLogins]
    @User_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Users]
    SET [Failed_Login_Count]   = 0,
        [Last_Failed_Login_At] = NULL,
        [Lockout_End_Utc]      = NULL
    WHERE [User_ID] = @User_ID;
END;
GO
