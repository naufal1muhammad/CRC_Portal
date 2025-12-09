CREATE PROCEDURE [dbo].[spStaffDocument_StaffNames]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        Staff_Name
    FROM [dbo].[StaffDocument]
    WHERE ISNULL(Staff_Name, '') <> ''
    ORDER BY Staff_Name;
END;
GO