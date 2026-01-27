CREATE PROCEDURE [dbo].[spStaff_GetById]
    @Staff_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        s.[Staff_ID],
        s.[Staff_Name],
        s.[Staff_NRIC],
        s.[Staff_BirthDate],
        s.[Staff_Age],
        s.[Staff_Phone],
        s.[Staff_Email],
        s.[Staff_Gender],
        s.[Staff_ResState],
        s.[Staff_ResCity],
        s.[Staff_ResPostcode],
        s.[Staff_AddLine1],
        s.[Staff_AddLine2],
        s.[Staff_Base],
        s.[Staff_Type],
        st.[StaffType_Name]
    FROM [dbo].[Staff] s
    LEFT JOIN [dbo].[LU_STAFFTYPE] st
        ON s.[Staff_Type] = st.[StaffType_ID]
    WHERE s.[Staff_ID] = @Staff_ID;
END;
