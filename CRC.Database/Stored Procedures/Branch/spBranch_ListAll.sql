CREATE PROCEDURE [dbo].[spBranch_ListAll]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Branch_ID],
        [Branch_Name],
        [Branch_Location],
        [Branch_State],
        [Branch_Status],
        [Organization_ID],
        [Organization_Name]
    FROM [dbo].[Branch]
    ORDER BY [Branch_Name];
END;