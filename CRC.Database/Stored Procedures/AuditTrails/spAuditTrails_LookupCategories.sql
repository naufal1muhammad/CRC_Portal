CREATE PROCEDURE [dbo].[spAuditTrails_LookupCategories]
	AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        AuditTrail_Category
    FROM dbo.AuditTrails
    WHERE AuditTrail_Category IS NOT NULL AND LTRIM(RTRIM(AuditTrail_Category)) <> ''
    ORDER BY AuditTrail_Category;
END
GO