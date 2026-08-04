CREATE PROCEDURE [dbo].[spStaffDocument_List]
    @Staff_ID VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.[StaffDocument_ID],
        d.[Staff_ID],
        s.[Staff_Name],
        d.[StaffDocumentType_ID],
        COALESCE(t.[StaffDocumentType_Name], d.[StaffDocumentType_ID]) AS [StaffDocumentType_Name],
        d.[FileName],
        d.[BlobName],
        d.[ContentType],
        d.[UploadedOn]
    FROM [dbo].[StaffDocument] d
    LEFT JOIN [dbo].[Staff] s
        ON UPPER(LTRIM(RTRIM(ISNULL(s.[Staff_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[Staff_ID], ''))))
    LEFT JOIN [dbo].[LU_STAFFDOCUMENTTYPE] t
        ON UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], ''))))
    WHERE (@Staff_ID IS NULL OR @Staff_ID = '' OR d.[Staff_ID] = @Staff_ID)
    ORDER BY d.[UploadedOn] DESC, d.[StaffDocument_ID] DESC;
END;
