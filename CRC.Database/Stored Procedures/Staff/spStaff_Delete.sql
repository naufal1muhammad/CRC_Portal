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

    DELETE FROM [dbo].[Staff]
    WHERE [Staff_ID] = @Staff_ID;

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
            'Staff',
            CONCAT(
                'Deleted Staff: Staff_ID=', @Staff_ID,
                '; Name=', ISNULL(@Staff_Name, '')
            )
        );
    END
END;
