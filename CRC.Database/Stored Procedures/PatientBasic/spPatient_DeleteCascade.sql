CREATE PROCEDURE [dbo].[spPatient_DeleteCascade]
(
    @Patient_ID VARCHAR(100),
    @User_ID    INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Patient_Name VARCHAR(100);

    SELECT @Patient_Name = [Patient_Name]
    FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;

    -- Delete children first
    DELETE FROM [dbo].[PatientAppointment]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientJourney]
    WHERE [Patient_ID] = @Patient_ID;

    -- -----------------------------
    -- Capture document blob keys BEFORE deleting PatientDocument rows,
    -- so the caller can remove the blobs after the rows are gone.
    -- -----------------------------
    DECLARE @DocBlobs TABLE ([BlobName] VARCHAR(500));

    INSERT INTO @DocBlobs ([BlobName])
    SELECT [BlobName]
    FROM [dbo].[PatientDocument]
    WHERE [Patient_ID] = @Patient_ID
      AND [BlobName] IS NOT NULL
      AND LEN(LTRIM(RTRIM([BlobName]))) > 0;

    DELETE FROM [dbo].[PatientDocument]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientFollowUp]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientColonoscopy]
    WHERE [Patient_ID] = @Patient_ID;

    DELETE FROM [dbo].[PatientAssessment]
    WHERE [Patient_ID] = @Patient_ID;

    -- Finally delete from master
    DELETE FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;

    DECLARE @RowsAffected INT = @@ROWCOUNT;

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
            'PatientBasic',
            CONCAT(
                'Deleted Patient: Patient_ID=', @Patient_ID,
                '; Name=', ISNULL(@Patient_Name, '')
            )
        );
    END

    -- Return the blob keys captured above so the caller can remove the blobs
    -- now that the rows are gone.
    SELECT [BlobName] FROM @DocBlobs;
END;
GO
