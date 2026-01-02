CREATE PROCEDURE [dbo].[spUsers_Register]
    @User_Name     VARCHAR(100),
    @Username      VARCHAR(100),
    @User_Email    VARCHAR(100),
    @PasswordHash  VARCHAR(500),
    @User_Type     INT,
    @Staff_ID      VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = @Username)
    BEGIN
        RAISERROR('Username already exists.', 16, 1);
        RETURN;
    END

    DECLARE @StaffIdFinal VARCHAR(100) = NULL;

    -- Only STAFF users must have Staff_ID
    IF @User_Type = 3
    BEGIN
        SET @StaffIdFinal = NULLIF(LTRIM(RTRIM(@Staff_ID)), '');

        IF @StaffIdFinal IS NULL
        BEGIN
            RAISERROR('Staff_ID is required for STAFF users.', 16, 1);
            RETURN;
        END

        -- Optional but recommended: validate Staff_ID exists in dbo.Staff
        IF NOT EXISTS (SELECT 1 FROM dbo.Staff WHERE Staff_ID = @StaffIdFinal)
        BEGIN
            RAISERROR('Invalid Staff_ID. Staff not found.', 16, 1);
            RETURN;
        END
    END

   INSERT INTO [dbo].[Users]
    ([User_Name], [Username], [User_Email], [Password_Hash], [User_Type], [Staff_ID], [Created_At], [Last_Login])
VALUES
    (@User_Name, @Username, @User_Email, @PasswordHash, @User_Type, @StaffIdFinal, GETUTCDATE(), GETUTCDATE());
END;
GO
