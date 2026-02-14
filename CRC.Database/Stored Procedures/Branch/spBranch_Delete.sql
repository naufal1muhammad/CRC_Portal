CREATE PROCEDURE [dbo].[spBranch_Delete]
    @Branch_ID VARCHAR(100),
    @User_ID   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Branch]
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
            'DELETE',
            'Branch',
            CONCAT('Deleted Branch: Branch_ID=', @Branch_ID)
        );
    END
END;