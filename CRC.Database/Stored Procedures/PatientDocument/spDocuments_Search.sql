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

    -- Normalise blanks => NULL
    SET @IndividualName = NULLIF(LTRIM(RTRIM(@IndividualName)), '');
    SET @DocumentType   = NULLIF(LTRIM(RTRIM(@DocumentType)),   '');

    IF (@Mode = 'Patient')
    BEGIN
        SELECT
            d.[Patient_ID]               AS [Id],
            d.[Patient_Name]             AS [Name],
            d.[PatientDocumentType_Name] AS [DocumentType],
            d.[FileName]                 AS [FileName],
            d.[FilePath]                 AS [FilePath],
            d.[UploadedOn]               AS [UploadedOn]
        FROM [dbo].[PatientDocument] d
        WHERE
            (@IndividualName IS NULL OR d.[Patient_Name] = @IndividualName)
            AND (@DocumentType IS NULL OR d.[PatientDocumentType_Name] = @DocumentType)
        ORDER BY
            d.[UploadedOn] DESC,
            d.[Patient_Name],
            d.[PatientDocumentType_Name];
    END
    ELSE IF (@Mode = 'Staff')
    BEGIN
        SELECT
            d.[Staff_ID]                 AS [Id],
            d.[Staff_Name]               AS [Name],
            d.[StaffDocumentType_Name]   AS [DocumentType],
            d.[FileName]                 AS [FileName],
            d.[FilePath]                 AS [FilePath],
            d.[UploadedOn]               AS [UploadedOn]
        FROM [dbo].[StaffDocument] d
        WHERE
            (@IndividualName IS NULL OR d.[Staff_Name] = @IndividualName)
            AND (@DocumentType IS NULL OR d.[StaffDocumentType_Name] = @DocumentType)
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