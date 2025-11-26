CREATE PROCEDURE [dbo].[spStaff_Insert]
    @Staff_ID     VARCHAR(100),
    @Staff_Name   VARCHAR(100),
    @Staff_NRIC   VARCHAR(100),
    @Staff_Phone  VARCHAR(50),
    @Staff_Email  VARCHAR(100),
    @Branch_ID    VARCHAR(100),
    @Branch_Name  VARCHAR(100),
    @Staff_Type   INT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Staff] WHERE [Staff_ID] = @Staff_ID)
    BEGIN
        RAISERROR('Staff ID already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Staff] (
        [Staff_ID],
        [Staff_Name],
        [Staff_NRIC],
        [Staff_Phone],
        [Staff_Email],
        [Branch_ID],
        [Branch_Name],
        [Staff_Type]
    )
    VALUES (
        @Staff_ID,
        @Staff_Name,
        @Staff_NRIC,
        @Staff_Phone,
        @Staff_Email,
        @Branch_ID,
        @Branch_Name,
        @Staff_Type
    );
END;