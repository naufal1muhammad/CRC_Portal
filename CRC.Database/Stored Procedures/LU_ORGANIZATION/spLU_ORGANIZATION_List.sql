CREATE PROCEDURE [dbo].[spLU_ORGANIZATION_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Organization_ID],
        [Organization_Name]
    FROM [dbo].[LU_ORGANIZATION]
    ORDER BY [Organization_Name];
END;