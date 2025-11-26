CREATE PROCEDURE [dbo].[spStaff_Update]
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

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Staff] WHERE [Staff_ID] = @Staff_ID)
    BEGIN
        RAISERROR('Staff not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Staff]
    SET
        [Staff_Name]  = @Staff_Name,
        [Staff_NRIC]  = @Staff_NRIC,
        [Staff_Phone] = @Staff_Phone,
        [Staff_Email] = @Staff_Email,
        [Branch_ID]   = @Branch_ID,
        [Branch_Name] = @Branch_Name,
        [Staff_Type]  = @Staff_Type
    WHERE [Staff_ID] = @Staff_ID;
END;