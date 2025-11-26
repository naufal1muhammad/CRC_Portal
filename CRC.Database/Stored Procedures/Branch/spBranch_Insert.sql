CREATE PROCEDURE [dbo].[spBranch_Insert]
    @Branch_ID        VARCHAR(100),
    @Branch_Name      VARCHAR(100),
    @Branch_Location  VARCHAR(200),
    @Branch_State     VARCHAR(100),
    @Branch_Status    BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Branch] WHERE [Branch_ID] = @Branch_ID)
    BEGIN
        RAISERROR('Branch ID already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Branch] (
        [Branch_ID],
        [Branch_Name],
        [Branch_Location],
        [Branch_State],
        [Branch_Status]
    )
    VALUES (
        @Branch_ID,
        @Branch_Name,
        @Branch_Location,
        @Branch_State,
        @Branch_Status
    );
END;