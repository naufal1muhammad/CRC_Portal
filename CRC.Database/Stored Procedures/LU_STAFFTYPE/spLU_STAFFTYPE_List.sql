CREATE PROCEDURE [dbo].[spLU_STAFFTYPE_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [StaffType_ID],
        [StaffType_Name]
    FROM [dbo].[LU_STAFFTYPE]
    ORDER BY [StaffType_Name];
END;