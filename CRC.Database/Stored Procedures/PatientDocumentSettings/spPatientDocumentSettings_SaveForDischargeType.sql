CREATE PROCEDURE [dbo].[spPatientDocumentSettings_SaveForDischargeType]
(
    @DischargeType_ID        VARCHAR(100),
    @PatientDocumentType_IDs NVARCHAR(MAX)  -- e.g. 'DOC001,DOC005,DOC010'
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DischargeType_Name VARCHAR(100);

    SELECT @DischargeType_Name = [DischargeType_Name]
    FROM [dbo].[LU_DISCHARGETYPE]
    WHERE [DischargeType_ID] = @DischargeType_ID;

    IF @DischargeType_Name IS NULL
    BEGIN
        RAISERROR('Invalid DischargeType_ID.', 11, 1);
        RETURN;
    END

    -- Clear existing settings for this discharge type
    DELETE FROM [dbo].[PatientDocumentSettings]
    WHERE [DischargeType_ID] = @DischargeType_ID;

    IF @PatientDocumentType_IDs IS NULL OR LTRIM(RTRIM(@PatientDocumentType_IDs)) = ''
    BEGIN
        -- Nothing else to insert
        RETURN;
    END

    ;WITH cte AS
    (
        SELECT LTRIM(RTRIM(value)) AS PatientDocumentType_ID
        FROM STRING_SPLIT(@PatientDocumentType_IDs, ',')
        WHERE LTRIM(RTRIM(value)) <> ''
    )
    INSERT INTO [dbo].[PatientDocumentSettings]
    (
        [DischargeType_ID],
        [DischargeType_Name],
        [PatientDocumentType_ID],
        [PatientDocumentType_Name]
    )
    SELECT
        @DischargeType_ID,
        @DischargeType_Name,
        c.PatientDocumentType_ID,
        t.PatientDocumentType_Name
    FROM cte c
    INNER JOIN [dbo].[LU_PATDOCUMENTTYPE] t
        ON t.PatientDocumentType_ID = c.PatientDocumentType_ID;
END;
GO