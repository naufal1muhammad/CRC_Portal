CREATE PROCEDURE [dbo].[spUsers_ValidateLogin]
    @Username VARCHAR(100),
    @Password VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 
        [User_ID],
        [User_Name],
        [Username],
        [User_Email],
        [User_Type]
    FROM [dbo].[Users]
    WHERE [Username] = @Username
      AND [Password] = @Password;
END;