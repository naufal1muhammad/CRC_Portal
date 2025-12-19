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
        [Password_Hash] AS PasswordHash,
        [User_Type],
        [Staff_ID]      AS StaffId
    FROM [dbo].[Users]
    WHERE [Username] = @Username;
END;
GO