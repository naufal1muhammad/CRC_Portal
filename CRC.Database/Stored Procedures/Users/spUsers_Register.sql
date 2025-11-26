CREATE PROCEDURE [dbo].[spUsers_Register]
    @User_Name  VARCHAR(100),
    @Username   VARCHAR(100),
    @User_Email VARCHAR(100),
    @Password   VARCHAR(200),
    @User_Type  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = @Username)
    BEGIN
        RAISERROR('Username already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Users] ([User_Name], [Username], [User_Email], [Password], [User_Type])
    VALUES (@User_Name, @Username, @User_Email, @Password, @User_Type);
END;