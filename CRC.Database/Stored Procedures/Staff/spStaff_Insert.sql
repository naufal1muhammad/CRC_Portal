CREATE PROCEDURE [dbo].[spStaff_Insert]
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
    @Staff_Type        VARCHAR(100),  -- this is StaffType_ID
    @User_ID           INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Staff_Type IS NULL OR LTRIM(RTRIM(@Staff_Type)) = ''
    BEGIN
        RAISERROR('Staff_Type (StaffType_ID) is required.', 16, 1);
        RETURN;
    END

    -- Global last-5-digit sequence
    DECLARE @LastNumber INT;

    SELECT @LastNumber =
        MAX(
            TRY_CAST(RIGHT([Staff_ID], 5) AS INT)
        )
    FROM [dbo].[Staff];

    IF @LastNumber IS NULL
        SET @LastNumber = 0;

    DECLARE @NextNumber INT = @LastNumber + 1;

    DECLARE @Suffix VARCHAR(5) =
        RIGHT('00000' + CAST(@NextNumber AS VARCHAR(5)), 5);

    DECLARE @Staff_ID VARCHAR(100) = @Staff_Type + '-' + @Suffix;

    INSERT INTO [dbo].[Staff]
    (
        [Staff_ID],
        [Staff_Name],
        [Staff_NRIC],
        [Staff_BirthDate],
        [Staff_Age],
        [Staff_Phone],
        [Staff_Email],
        [Staff_Gender],
        [Staff_ResState],
        [Staff_ResCity],
        [Staff_ResPostcode],
        [Staff_AddLine1],
        [Staff_AddLine2],
        [Staff_Base],
        [Staff_Type]
    )
    VALUES
    (
        @Staff_ID,
        @Staff_Name,
        @Staff_NRIC,
        @Staff_BirthDate,
        @Staff_Age,
        @Staff_Phone,
        @Staff_Email,
        @Staff_Gender,
        @Staff_ResState,
        @Staff_ResCity,
        @Staff_ResPostcode,
        @Staff_AddLine1,
        @Staff_AddLine2,
        @Staff_Base,
        @Staff_Type
    );

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
        'Staff',
        CONCAT(
            'Created Staff: Staff_ID=', @Staff_ID,
            '; Name=', @Staff_Name,
            '; NRIC=', @Staff_NRIC,
            '; Phone=', @Staff_Phone,
            '; Email=', @Staff_Email,
            '; Type=', @Staff_Type
        )
    );

    -- Return the new Staff_ID to C#
    SELECT @Staff_ID AS NewStaff_ID;
END;
