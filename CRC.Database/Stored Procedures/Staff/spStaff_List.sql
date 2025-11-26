CREATE PROCEDURE [dbo].[spStaff_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Staff_ID],
        [Staff_Name],
        [Staff_NRIC],
        [Staff_Phone],
        [Staff_Email],
        [Branch_ID],
        [Branch_Name],
        [Staff_Type]
    FROM [dbo].[Staff]
    ORDER BY [Staff_Name];
END;