CREATE PROCEDURE [dbo].[spDocuments_Search]
(
    @Mode           VARCHAR(10),      -- 'Patient' or 'Staff'
    @IndividualName VARCHAR(200) = NULL,
    @DocumentType   VARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    SET @Mode = UPPER(LTRIM(RTRIM(ISNULL(@Mode, ''))));

    SET @IndividualName = NULLIF(LTRIM(RTRIM(@IndividualName)), '');
    SET @DocumentType   = NULLIF(LTRIM(RTRIM(@DocumentType)),   '');

    DECLARE @IndividualNameU VARCHAR(200) = CASE WHEN @IndividualName IS NULL THEN NULL ELSE UPPER(@IndividualName) END;
    DECLARE @DocumentTypeU   VARCHAR(200) = CASE WHEN @DocumentType IS NULL THEN NULL ELSE UPPER(@DocumentType) END;

    IF (@Mode = 'PATIENT')
    BEGIN
        SELECT
            d.[Patient_ID] AS [Id],
            pb.[Patient_Name] AS [Name],
            COALESCE(NULLIF(LTRIM(RTRIM(t.[PatientDocumentType_Name])), ''), NULLIF(LTRIM(RTRIM(d.[PatientDocumentType_ID])), '')) AS [DocumentType],
            d.[FileName] AS [FileName],
            d.[BlobName] AS [BlobName],
            d.[UploadedOn] AS [UploadedOn]
        FROM [dbo].[PatientDocument] d
        LEFT JOIN [dbo].[PatientBasic] pb
            ON pb.[Patient_ID] = d.[Patient_ID]
        LEFT JOIN [dbo].[LU_PATDOCUMENTTYPE] t
            ON UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[PatientDocumentType_ID], ''))))
        WHERE
            (@IndividualNameU IS NULL OR UPPER(LTRIM(RTRIM(ISNULL(pb.[Patient_Name], '')))) = @IndividualNameU)
            AND (
                @DocumentTypeU IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_Name], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(t.[PatientDocumentType_ID], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[PatientDocumentType_ID], '')))) = @DocumentTypeU
            )
        ORDER BY
            TRY_CONVERT(DATETIME, d.[UploadedOn], 120) DESC,
            pb.[Patient_Name],
            t.[PatientDocumentType_Name];
    END
    ELSE IF (@Mode = 'STAFF')
    BEGIN
        SELECT
            d.[Staff_ID] AS [Id],
            s.[Staff_Name] AS [Name],
            COALESCE(NULLIF(LTRIM(RTRIM(t.[StaffDocumentType_Name])), ''), NULLIF(LTRIM(RTRIM(d.[StaffDocumentType_ID])), '')) AS [DocumentType],
            d.[FileName] AS [FileName],
            d.[BlobName] AS [BlobName],
            CONVERT(VARCHAR(100), d.[UploadedOn], 120) AS [UploadedOn]
        FROM [dbo].[StaffDocument] d
        LEFT JOIN [dbo].[Staff] s
            ON UPPER(LTRIM(RTRIM(ISNULL(s.[Staff_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[Staff_ID], ''))))
        LEFT JOIN [dbo].[LU_STAFFDOCUMENTTYPE] t
            ON UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], '')))) = UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], ''))))
        WHERE
            (@IndividualNameU IS NULL OR UPPER(LTRIM(RTRIM(ISNULL(s.[Staff_Name], '')))) = @IndividualNameU)
            AND (
                @DocumentTypeU IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_Name], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(t.[StaffDocumentType_ID], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], '')))) = @DocumentTypeU
            )
        ORDER BY
            d.[UploadedOn] DESC,
            s.[Staff_Name],
            t.[StaffDocumentType_Name];
    END
    ELSE
    BEGIN
        SELECT
            CAST(NULL AS VARCHAR(100)) AS [Id],
            CAST(NULL AS VARCHAR(200)) AS [Name],
            CAST(NULL AS VARCHAR(200)) AS [DocumentType],
            CAST(NULL AS VARCHAR(255)) AS [FileName],
            CAST(NULL AS VARCHAR(500)) AS [BlobName],
            CAST(NULL AS VARCHAR(100)) AS [UploadedOn]
        WHERE 1 = 0;
    END
END;
GO
