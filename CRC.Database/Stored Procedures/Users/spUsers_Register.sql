CREATE PROCEDURE [dbo].[spUsers_Register]
    @User_Name     VARCHAR(100),
    @Username      VARCHAR(100),
    @User_Email    VARCHAR(100),
    @PasswordHash  VARCHAR(500),
    @User_Type     INT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = @Username)
    BEGIN
        RAISERROR('Username already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Users] ([User_Name], [Username], [User_Email], [Password_Hash], [User_Type])
    VALUES (@User_Name, @Username, @User_Email, @PasswordHash, @User_Type);
END;
GO