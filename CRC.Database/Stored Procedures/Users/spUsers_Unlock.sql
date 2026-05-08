CREATE PROCEDURE [dbo].[spUsers_Unlock]
    @User_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [User_ID] = @User_ID)
    BEGIN
        RAISERROR('User not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Users]
    SET [Failed_Login_Count]   = 0,
        [Last_Failed_Login_At] = NULL,
        [Lockout_End_Utc]      = NULL
    WHERE [User_ID] = @User_ID;
END;
GO
