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
        Last_Login
    FROM dbo.Users
    ORDER BY User_ID DESC;
END
GO