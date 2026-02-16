CREATE PROCEDURE [dbo].[spAuditTrails_LookupActions]
	AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        AuditTrail_Action
    FROM dbo.AuditTrails
    WHERE AuditTrail_Action IS NOT NULL AND LTRIM(RTRIM(AuditTrail_Action)) <> ''
    ORDER BY AuditTrail_Action;
END
GO