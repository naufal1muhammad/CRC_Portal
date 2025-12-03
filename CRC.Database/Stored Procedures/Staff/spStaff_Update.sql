CREATE PROCEDURE [dbo].[spStaff_Update]
    @Staff_ID    VARCHAR(100),
    @Staff_Name  VARCHAR(100),
    @Staff_NRIC  VARCHAR(100),
    @Staff_Phone VARCHAR(100),
    @Staff_Email VARCHAR(100),
    @Branch_ID   VARCHAR(100),
    @Branch_Name VARCHAR(100),
    @Staff_Type  VARCHAR(100)  -- still pass it, but UI will keep it fixed
AS
BEGIN
    SET NOCOUNT ON;

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