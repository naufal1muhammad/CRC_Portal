CREATE PROCEDURE [dbo].[spStaffDocument_StaffNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        s.[Staff_Name]
    FROM [dbo].[StaffDocument] d
    INNER JOIN [dbo].[Staff] s
        ON UPPER(LTRIM(RTRIM(ISNULL(s.[Staff_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[Staff_ID], ''))))
    WHERE ISNULL(s.[Staff_Name], '') <> ''
    ORDER BY s.[Staff_Name];
END;
GO
