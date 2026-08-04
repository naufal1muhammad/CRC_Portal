CREATE PROCEDURE [dbo].[spStaffDocument_Delete]
(
    @StaffDocument_ID INT,
    @User_ID          INT = NULL,
    -- The caller uses this to delete the corresponding BLOB after the row is gone.
    -- A NULL means no row was deleted and nothing should be removed from storage.
    @DeletedBlobName  VARCHAR(500) = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Capture details for audit (best effort)
    DECLARE
        @Staff_ID VARCHAR(100),
        @Staff_Name VARCHAR(100),
        @StaffDocumentType_ID VARCHAR(100),
        @StaffDocumentType_Name VARCHAR(100),
        @FileName VARCHAR(255),
        @BlobName VARCHAR(500),
        @ContentType VARCHAR(100),
        @UploadedOn VARCHAR(100);

    SELECT
        @Staff_ID = [Staff_ID],
        @StaffDocumentType_ID = [StaffDocumentType_ID],
        @FileName = [FileName],
        @BlobName = [BlobName],
        @ContentType = [ContentType],
        @UploadedOn = CONVERT(VARCHAR(100), [UploadedOn], 120)
    FROM [dbo].[StaffDocument]
    WHERE [StaffDocument_ID] = @StaffDocument_ID;

    -- Lookups (best effort)
    SELECT
        @Staff_Name = [Staff_Name]
    FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;

    SELECT
        @StaffDocumentType_Name = [StaffDocumentType_Name]
    FROM [dbo].[LU_STAFFDOCUMENTTYPE]
    WHERE [StaffDocumentType_ID] = @StaffDocumentType_ID;

    DELETE FROM [dbo].[StaffDocument]
    WHERE [StaffDocument_ID] = @StaffDocument_ID;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

    SET @DeletedBlobName = CASE WHEN @RowsAffected > 0 THEN @BlobName ELSE NULL END;

    IF @RowsAffected > 0
    BEGIN
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
            'DELETE',
            'StaffDocument',
            CONCAT(
                'Deleted Staff Document: StaffDocument_ID=', @StaffDocument_ID,
                '; Staff_ID=', ISNULL(@Staff_ID, ''),
                '; Staff_Name=', ISNULL(@Staff_Name, ''),
                '; DocType=', ISNULL(@StaffDocumentType_Name, ''), ' (', ISNULL(@StaffDocumentType_ID, ''), ')',
                '; FileName=', ISNULL(@FileName, ''),
                '; ContentType=', ISNULL(@ContentType, ''),
                '; UploadedOn=', ISNULL(@UploadedOn, '')
            )
        );
    END
END;
GO
