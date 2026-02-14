CREATE PROCEDURE [dbo].[spBranch_Update]
    @Branch_ID         VARCHAR(100),
    @Branch_Name       VARCHAR(100),
    @Branch_Location   VARCHAR(100),
    @Branch_State      VARCHAR(100),
    @Branch_Status     BIT,
    @Organization_ID   VARCHAR(100),
    @Organization_Name VARCHAR(100),
    @User_ID           INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Update main Branch
    UPDATE [dbo].[Branch]
    SET
        [Branch_Name]       = @Branch_Name,
        [Branch_Location]   = @Branch_Location,
        [Branch_State]      = @Branch_State,
        [Branch_Status]     = @Branch_Status,
        [Organization_ID]   = @Organization_ID,
        [Organization_Name] = @Organization_Name
    WHERE [Branch_ID] = @Branch_ID;

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
            'UPDATE',
            'Branch',
            CONCAT('Updated Branch: Branch_ID=', @Branch_ID, '; Name=', @Branch_Name, '; Org_ID=', @Organization_ID)
        );
    END
END;
GO