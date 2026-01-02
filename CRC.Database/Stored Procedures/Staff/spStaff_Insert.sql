CREATE PROCEDURE [dbo].[spStaff_Insert]
    @Staff_Name   VARCHAR(100),
    @Staff_NRIC   VARCHAR(100),
    @Staff_Phone  VARCHAR(100),
    @Staff_Email  VARCHAR(100),
    @Branch_ID    VARCHAR(100),
    @Branch_Name  VARCHAR(100),
    @Staff_Type   VARCHAR(100)  -- this is StaffType_ID
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
        [Staff_Phone],
        [Staff_Email],
        [Branch_ID],
        [Branch_Name],
        [Staff_Type]
    )
    VALUES
    (
        @Staff_ID,
        @Staff_Name,
        @Staff_NRIC,
        @Staff_Phone,
        @Staff_Email,
        @Branch_ID,
        @Branch_Name,
        @Staff_Type
    );

    -- Return the new Staff_ID to C#
    SELECT @Staff_ID AS NewStaff_ID;
END;