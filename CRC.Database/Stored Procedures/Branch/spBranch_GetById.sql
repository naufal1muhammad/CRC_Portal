CREATE PROCEDURE [dbo].[spBranch_GetById]
    @Branch_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [Branch_ID],
        [Branch_Name],
        [Branch_Location],
        [Branch_State],
        [Branch_Status]
    FROM [dbo].[Branch]
    WHERE [Branch_ID] = @Branch_ID;
END;