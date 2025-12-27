CREATE PROCEDURE dbo.spUsers_UpdateLastLogin
    @User_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET Last_Login = GETUTCDATE()
    WHERE User_ID = @User_ID;
END
GO