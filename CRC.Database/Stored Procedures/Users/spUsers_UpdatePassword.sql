CREATE PROCEDURE [dbo].[spUsers_UpdatePassword]
    @User_ID INT,
    @PasswordHash VARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [User_ID] = @User_ID)
    BEGIN
        RAISERROR('User not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Users]
    SET [Password_Hash] = @PasswordHash
    WHERE [User_ID] = @User_ID;
END;
GO