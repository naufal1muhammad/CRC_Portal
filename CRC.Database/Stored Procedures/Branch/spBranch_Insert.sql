CREATE PROCEDURE [dbo].[spBranch_Insert]
    @Branch_Name       VARCHAR(100),
    @Branch_Location   VARCHAR(100),
    @Branch_State      VARCHAR(100),
    @Branch_Status     BIT,
    @Organization_ID   VARCHAR(100),
    @Organization_Name VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Organization_ID IS NULL OR LTRIM(RTRIM(@Organization_ID)) = ''
    BEGIN
        RAISERROR('Organization_ID is required.', 16, 1);
        RETURN;
    END

    IF @Branch_State IS NULL OR LTRIM(RTRIM(@Branch_State)) = ''
    BEGIN
        RAISERROR('Branch_State is required.', 16, 1);
        RETURN;
    END

    DECLARE @State_ID INT;

    SELECT @State_ID = [State_ID]
    FROM [dbo].[LU_STATES]
    WHERE [State_Name] = @Branch_State;

    IF @State_ID IS NULL
    BEGIN
        RAISERROR('Invalid Branch_State. No matching State_ID found in LU_STATES.', 16, 1);
        RETURN;
    END

    DECLARE @StateIDText VARCHAR(2) =
        RIGHT('00' + CAST(@State_ID AS VARCHAR(2)), 2);

    DECLARE @Prefix VARCHAR(50) =
        @Organization_ID + @StateIDText;

       DECLARE @LastNumber INT;

    SELECT @LastNumber =
        MAX(
            TRY_CAST(RIGHT([Branch_ID], 3) AS INT)
        )
    FROM [dbo].[Branch];

    IF @LastNumber IS NULL
        SET @LastNumber = 0;

    DECLARE @NextNumber INT = @LastNumber + 1;

    DECLARE @Suffix VARCHAR(3) =
        RIGHT('000' + CAST(@NextNumber AS VARCHAR(3)), 3);

    DECLARE @Branch_ID VARCHAR(100) = @Prefix + @Suffix;

    INSERT INTO [dbo].[Branch]
    (
        [Branch_ID],
        [Branch_Name],
        [Branch_Location],
        [Branch_State],
        [Branch_Status],
        [Organization_ID],
        [Organization_Name]
    )
    VALUES
    (
        @Branch_ID,
        @Branch_Name,
        @Branch_Location,
        @Branch_State,
        @Branch_Status,
        @Organization_ID,
        @Organization_Name
    );

    -- Return the new Branch_ID (so C# can know it if needed)
    SELECT @Branch_ID AS NewBranch_ID;
END;