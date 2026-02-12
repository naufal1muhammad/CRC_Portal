-- This stored procedure is used to query the search function under Documents page, which queries two tables, PatientDocument and StaffDocument.
CREATE PROCEDURE [dbo].[spDocuments_Search]
(
    @Mode           VARCHAR(10),      -- 'Patient' or 'Staff'
    @IndividualName VARCHAR(200) = NULL,
    @DocumentType   VARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Normalise mode to guard against case-sensitive collations & trailing spaces.
    SET @Mode = UPPER(LTRIM(RTRIM(ISNULL(@Mode, ''))));

    -- Normalise blanks => NULL
    SET @IndividualName = NULLIF(LTRIM(RTRIM(@IndividualName)), '');
    SET @DocumentType   = NULLIF(LTRIM(RTRIM(@DocumentType)),   '');

    DECLARE @IndividualNameU VARCHAR(200) = CASE WHEN @IndividualName IS NULL THEN NULL ELSE UPPER(@IndividualName) END;
    DECLARE @DocumentTypeU   VARCHAR(200) = CASE WHEN @DocumentType IS NULL THEN NULL ELSE UPPER(@DocumentType) END;

    IF (@Mode = 'PATIENT')
    BEGIN
        SELECT
            d.[Patient_ID] AS [Id],
            d.[Patient_Name] AS [Name],
            COALESCE(NULLIF(LTRIM(RTRIM(d.[PatientDocumentType_Name])), ''), NULLIF(LTRIM(RTRIM(d.[PatientDocumentType_ID])), '')) AS [DocumentType],
            d.[FileName] AS [FileName],
            d.[FilePath] AS [FilePath],
            d.[UploadedOn] AS [UploadedOn]
        FROM [dbo].[PatientDocument] d
        WHERE
            (@IndividualNameU IS NULL OR UPPER(LTRIM(RTRIM(ISNULL(d.[Patient_Name], '')))) = @IndividualNameU)
            AND (
                @DocumentTypeU IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[PatientDocumentType_Name], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[PatientDocumentType_ID], '')))) = @DocumentTypeU
            )
        ORDER BY
            TRY_CONVERT(DATETIME, d.[UploadedOn], 120) DESC,
            d.[Patient_Name],
            d.[PatientDocumentType_Name];
    END
    ELSE IF (@Mode = 'STAFF')
    BEGIN
        SELECT
            d.[Staff_ID] AS [Id],
            d.[Staff_Name] AS [Name],
            COALESCE(NULLIF(LTRIM(RTRIM(d.[StaffDocumentType_Name])), ''), NULLIF(LTRIM(RTRIM(d.[StaffDocumentType_ID])), '')) AS [DocumentType],
            d.[FileName] AS [FileName],
            d.[FilePath] AS [FilePath],
            CONVERT(VARCHAR(100), d.[UploadedOn], 120) AS [UploadedOn]
        FROM [dbo].[StaffDocument] d
        WHERE
            (@IndividualNameU IS NULL OR UPPER(LTRIM(RTRIM(ISNULL(d.[Staff_Name], '')))) = @IndividualNameU)
            AND (
                @DocumentTypeU IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_Name], '')))) = @DocumentTypeU
                OR UPPER(LTRIM(RTRIM(ISNULL(d.[StaffDocumentType_ID], '')))) = @DocumentTypeU
            )
        ORDER BY
            d.[UploadedOn] DESC,
            d.[Staff_Name],
            d.[StaffDocumentType_Name];
    END
    ELSE
    BEGIN
        -- Invalid mode: return empty set
        SELECT
            CAST(NULL AS VARCHAR(100)) AS [Id],
            CAST(NULL AS VARCHAR(200)) AS [Name],
            CAST(NULL AS VARCHAR(200)) AS [DocumentType],
            CAST(NULL AS VARCHAR(255)) AS [FileName],
            CAST(NULL AS VARCHAR(500)) AS [FilePath],
            CAST(NULL AS VARCHAR(100)) AS [UploadedOn]
        WHERE 1 = 0;
    END
END;
GO