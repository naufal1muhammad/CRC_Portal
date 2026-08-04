CREATE PROCEDURE [dbo].[spStaff_Delete]
    @Staff_ID VARCHAR(100),
    @User_ID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Staff_Name VARCHAR(100);

    SELECT @Staff_Name = [Staff_Name]
    FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;

    IF @Staff_Name IS NULL
    BEGIN
        SELECT
            CAST('NotFound' AS VARCHAR(20))   AS [Status],
            CAST('Staff not found.' AS VARCHAR(500)) AS [Message];

        SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [BlobName];
        RETURN;
    END

    -- -----------------------------
    -- Reference checks: block deletion when this Staff_ID is used
    -- in PatientAppointment / PatientJourney / PatientJourneyAudit.
    -- -----------------------------
    DECLARE @InAppointments   BIT = 0;
    DECLARE @InJourney        BIT = 0;
    DECLARE @InJourneyAudit   BIT = 0;

    IF EXISTS (SELECT 1 FROM [dbo].[PatientAppointment] WHERE [Staff_ID] = @Staff_ID)
        SET @InAppointments = 1;

    IF EXISTS (SELECT 1 FROM [dbo].[PatientJourney] WHERE [Staff_ID] = @Staff_ID)
        SET @InJourney = 1;

    IF EXISTS (SELECT 1 FROM [dbo].[PatientJourneyAudit] WHERE [Staff_ID] = @Staff_ID)
        SET @InJourneyAudit = 1;

    IF @InAppointments = 1 OR @InJourney = 1 OR @InJourneyAudit = 1
    BEGIN
        DECLARE @BlockedTables VARCHAR(500) = '';

        IF @InAppointments = 1
            SET @BlockedTables = @BlockedTables + ', Patient Appointments';
        IF @InJourney = 1
            SET @BlockedTables = @BlockedTables + ', Patient Journey';
        IF @InJourneyAudit = 1
            SET @BlockedTables = @BlockedTables + ', Patient Journey Audit';

        IF LEN(@BlockedTables) >= 2
            SET @BlockedTables = SUBSTRING(@BlockedTables, 3, LEN(@BlockedTables));

        DECLARE @BlockedMessage VARCHAR(500) =
            CONCAT(
                'Cannot delete this staff because they are still referenced by: ',
                @BlockedTables,
                '.'
            );

        SELECT
            CAST('Blocked' AS VARCHAR(20)) AS [Status],
            CAST(@BlockedMessage AS VARCHAR(500)) AS [Message];

        SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [BlobName];
        RETURN;
    END

    -- -----------------------------
    -- Capture document blob keys BEFORE deleting StaffDocument rows,
    -- so the caller can remove the blobs after the transaction commits.
    -- -----------------------------
    DECLARE @DocFiles TABLE ([BlobName] VARCHAR(500));

    INSERT INTO @DocFiles ([BlobName])
    SELECT [BlobName]
    FROM [dbo].[StaffDocument]
    WHERE [Staff_ID] = @Staff_ID
      AND [BlobName] IS NOT NULL
      AND LEN(LTRIM(RTRIM([BlobName]))) > 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        DELETE FROM [dbo].[StaffSlots]
        WHERE [Staff_ID] = @Staff_ID;

        DELETE FROM [dbo].[StaffDocument]
        WHERE [Staff_ID] = @Staff_ID;

        DELETE FROM [dbo].[Users]
        WHERE [Staff_ID] = @Staff_ID;

        DELETE FROM [dbo].[Staff]
        WHERE [Staff_ID] = @Staff_ID;

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
            'Staff',
            CONCAT(
                'Deleted Staff: Staff_ID=', @Staff_ID,
                '; Name=', ISNULL(@Staff_Name, ''),
                '; CascadedStaffSlots=Yes',
                '; CascadedStaffDocuments=Yes',
                '; CascadedUsers=Yes'
            )
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT
        CAST('Success' AS VARCHAR(20)) AS [Status],
        CAST('Staff deleted successfully.' AS VARCHAR(500)) AS [Message];

    SELECT [BlobName] FROM @DocFiles;
END;
