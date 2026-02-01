CREATE PROCEDURE [dbo].[spStaff_List]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.[Staff_ID],
        s.[Staff_Name],
        s.[Staff_NRIC],
        s.[Staff_Phone],
        s.[Staff_Email],
        s.[Staff_Type],
        st.[StaffType_Name]
    FROM [dbo].[Staff] s
    LEFT JOIN [dbo].[LU_STAFFTYPE] st
        ON s.[Staff_Type] = st.[StaffType_ID]
    ORDER BY s.[Staff_Name];
END;
