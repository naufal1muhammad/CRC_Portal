CREATE PROCEDURE [dbo].[spStaffDocumentSettings_GetByStaffType]
    @StaffType_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.[StaffDocumentType_ID],
        d.[StaffDocumentType_Name],
        CASE
            WHEN s.[StaffDocumentType_ID] IS NULL THEN 0
            ELSE 1
        END AS [IsMandatory]
    FROM [dbo].[LU_STAFFDOCUMENTTYPE] d
    LEFT JOIN [dbo].[StaffDocumentSettings] s
        ON s.[StaffType_ID] = @StaffType_ID
       AND s.[StaffDocumentType_ID] = d.[StaffDocumentType_ID]
    ORDER BY d.[StaffDocumentType_Name];
END;