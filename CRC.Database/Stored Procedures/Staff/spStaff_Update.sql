CREATE PROCEDURE [dbo].[spStaff_Update]
(
    @Staff_ID          VARCHAR(100),
    @Staff_Name        VARCHAR(100),
    @Staff_NRIC        VARCHAR(100),
    @Staff_BirthDate   DATETIME,
    @Staff_Age         INT,
    @Staff_Phone       VARCHAR(100),
    @Staff_Email       VARCHAR(100),
    @Staff_Gender      VARCHAR(100),
    @Staff_ResState    VARCHAR(100),
    @Staff_ResCity     VARCHAR(100),
    @Staff_ResPostcode VARCHAR(100),
    @Staff_AddLine1    VARCHAR(MAX),
    @Staff_AddLine2    VARCHAR(MAX),
    @Staff_Base        VARCHAR(100),
    @Staff_Type        VARCHAR(100),  -- still pass it, but UI will keep it fixed
    @User_ID           INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    ---------------------------------
    -- 1) Update main Staff
    ---------------------------------
    UPDATE [dbo].[Staff]
    SET
        [Staff_Name]        = @Staff_Name,
        [Staff_NRIC]        = @Staff_NRIC,
        [Staff_BirthDate]   = @Staff_BirthDate,
        [Staff_Age]         = @Staff_Age,
        [Staff_Phone]       = @Staff_Phone,
        [Staff_Email]       = @Staff_Email,
        [Staff_Gender]      = @Staff_Gender,
        [Staff_ResState]    = @Staff_ResState,
        [Staff_ResCity]     = @Staff_ResCity,
        [Staff_ResPostcode] = @Staff_ResPostcode,
        [Staff_AddLine1]    = @Staff_AddLine1,
        [Staff_AddLine2]    = @Staff_AddLine2,
        [Staff_Base]        = @Staff_Base,
        [Staff_Type]        = @Staff_Type
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
            'UPDATE',
            'Staff',
            CONCAT(
                'Updated Staff: Staff_ID=', @Staff_ID,
                '; Name=', @Staff_Name,
                '; NRIC=', @Staff_NRIC,
                '; Phone=', @Staff_Phone,
                '; Email=', @Staff_Email,
                '; Type=', @Staff_Type
            )
        );
    END
END;
GO
