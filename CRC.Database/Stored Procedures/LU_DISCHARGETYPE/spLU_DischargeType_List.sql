CREATE PROCEDURE [dbo].[spLU_DischargeType_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [DischargeType_ID], [DischargeType_Name]
    FROM [dbo].[LU_DISCHARGETYPE]
    ORDER BY [DischargeType_Name];
END;
GO