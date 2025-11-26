CREATE PROCEDURE [dbo].[spStaff_GetById]
    @Staff_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [Staff_ID],
        [Staff_Name],
        [Staff_NRIC],
        [Staff_Phone],
        [Staff_Email],
        [Branch_ID],
        [Branch_Name],
        [Staff_Type]
    FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;
END;