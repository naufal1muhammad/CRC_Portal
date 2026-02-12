CREATE PROCEDURE [dbo].[spStaffDocument_GetById]
    @StaffDocument_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        d.[StaffDocument_ID],
        d.[Staff_ID],
        s.[Staff_Name],
        d.[StaffDocumentType_ID],
        COALESCE(t.[StaffDocumentType_Name], d.[StaffDocumentType_ID]) AS [StaffDocumentType_Name],
        d.[FileName],
        d.[FilePath],
        d.[ContentType],
        d.[UploadedOn]
    FROM [dbo].[StaffDocument] d
    LEFT JOIN [dbo].[Staff] s
        ON UPPER(LTRIM(RTRIM(ISNULL(s.[Staff_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[Staff_ID], ''))))
    LEFT JOIN [dbo].[LU_STAFFDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], ''))))
    WHERE d.[StaffDocument_ID] = @StaffDocument_ID;
END;
