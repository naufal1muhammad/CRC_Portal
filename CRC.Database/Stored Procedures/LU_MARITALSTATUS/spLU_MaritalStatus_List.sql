CREATE PROCEDURE [dbo].[spLU_MaritalStatus_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [MaritalStatus_ID], [MaritalStatus_Name]
    FROM [dbo].[LU_MARITALSTATUS]
    ORDER BY [MaritalStatus_Name];
END;
GO