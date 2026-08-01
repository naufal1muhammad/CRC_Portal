CREATE PROCEDURE [dbo].[spStaffDocument_Insert]
(
    @Staff_ID               VARCHAR(100),
    @Staff_Name             VARCHAR(100),
    @StaffDocumentType_ID   VARCHAR(100),
    @StaffDocumentType_Name VARCHAR(100),
    @FileName               VARCHAR(255),
    @FilePath               VARCHAR(500),
    @ContentType            VARCHAR(100),
    @User_ID                INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[StaffDocument]
    (
        [Staff_ID],
        [StaffDocumentType_ID],
        [FileName],
        [FilePath],
        [ContentType],
        [UploadedOn]
    )
    VALUES
    (
        @Staff_ID,
        @StaffDocumentType_ID,
        @FileName,
        @FilePath,
        @ContentType,
        CAST(GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time' AS DATETIME)
    );

    DECLARE @InsertedStaffDocumentId INT = CONVERT(INT, SCOPE_IDENTITY());

    -- -----------------------------
    -- Audit trail
    -- -----------------------------
    INSERT INTO [dbo].[AuditTrails]
    (
        [User_Id],
        [AuditTrail_Action],
        [AuditTrail_Category],
        [AuditTrail_Summary]
    )
    VALUES
    (
        ISNULL(@User_ID, 0),
        'INSERT',
        'StaffDocument',
        CONCAT(
            'Uploaded Staff Document: StaffDocument_ID=', @InsertedStaffDocumentId,
            '; Staff_ID=', @Staff_ID,
            '; Staff_Name=', ISNULL(@Staff_Name, ''),
            '; DocType=', ISNULL(@StaffDocumentType_Name, ''), ' (', ISNULL(@StaffDocumentType_ID, ''), ')',
            '; FileName=', @FileName,
            '; ContentType=', @ContentType
        )
    );
END;
GO
