CREATE PROCEDURE [dbo].[spLU_STATES_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [State_ID], [State_Name]
    FROM [dbo].[LU_STATES]
    ORDER BY [State_Name];
END;