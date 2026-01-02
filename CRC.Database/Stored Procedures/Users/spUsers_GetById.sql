CREATE PROCEDURE [dbo].[spUsers_GetById]
    @User_ID INT
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
        [Staff_ID]      AS StaffId,
        [Created_At],
        [Last_Login]
    FROM [dbo].[Users]
    WHERE [User_ID] = @User_ID;
END;
GO