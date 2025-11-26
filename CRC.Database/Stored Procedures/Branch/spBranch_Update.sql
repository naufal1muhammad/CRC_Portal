CREATE PROCEDURE [dbo].[spBranch_Update]
    @Branch_ID        VARCHAR(100),
    @Branch_Name      VARCHAR(100),
    @Branch_Location  VARCHAR(200),
    @Branch_State     VARCHAR(100),
    @Branch_Status    BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Branch] WHERE [Branch_ID] = @Branch_ID)
    BEGIN
        RAISERROR('Branch not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Branch]
    SET
        [Branch_Name]     = @Branch_Name,
        [Branch_Location] = @Branch_Location,
        [Branch_State]    = @Branch_State,
        [Branch_Status]   = @Branch_Status
    WHERE [Branch_ID] = @Branch_ID;
END;