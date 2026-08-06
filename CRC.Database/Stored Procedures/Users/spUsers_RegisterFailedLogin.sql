CREATE PROCEDURE [dbo].[spUsers_RegisterFailedLogin]
    @Username             VARCHAR(100),
    @MaxFailedAttempts    INT,
    @LockoutMinutes       INT,
    @AttemptWindowMinutes INT,
    @NowUtc               DATETIME = NULL,
    @LockoutTriggered     BIT      = NULL OUTPUT,
    @LockoutEndUtc        DATETIME = NULL OUTPUT,
    @FailedLoginCount     INT      = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @NowUtc IS NULL SET @NowUtc = GETUTCDATE();

    SET @LockoutTriggered = 0;
    SET @LockoutEndUtc = NULL;
    SET @FailedLoginCount = 0;

    DECLARE @UserId INT;
    DECLARE @CurrentCount INT;
    DECLARE @LastFailedAt DATETIME;
    DECLARE @ExistingLockoutEnd DATETIME;

    SELECT
        @UserId              = [User_ID],
        @CurrentCount        = ISNULL([Failed_Login_Count], 0),
        @LastFailedAt        = [Last_Failed_Login_At],
        @ExistingLockoutEnd  = [Lockout_End_Utc]
    FROM [dbo].[Users]
    WHERE [Username] = @Username;

    -- No such user: caller already handles this (do not leak info).
    IF @UserId IS NULL
        RETURN;

    -- If a lockout window is still active, just return that state and don't bump the counter.
    IF @ExistingLockoutEnd IS NOT NULL AND @ExistingLockoutEnd > @NowUtc
    BEGIN
        SET @LockoutTriggered = 1;
        SET @LockoutEndUtc = @ExistingLockoutEnd;
        SET @FailedLoginCount = @CurrentCount;
        RETURN;
    END

    -- If the previous failure is older than the attempt window, reset the counter.
    IF @LastFailedAt IS NULL
       OR @AttemptWindowMinutes <= 0
       OR DATEADD(MINUTE, @AttemptWindowMinutes, @LastFailedAt) < @NowUtc
    BEGIN
        SET @CurrentCount = 0;
    END

    SET @CurrentCount = @CurrentCount + 1;

    DECLARE @NewLockoutEnd DATETIME = NULL;
    IF @MaxFailedAttempts > 0 AND @CurrentCount >= @MaxFailedAttempts
    BEGIN
        SET @NewLockoutEnd = DATEADD(MINUTE, @LockoutMinutes, @NowUtc);
        SET @LockoutTriggered = 1;
    END

    UPDATE [dbo].[Users]
    SET [Failed_Login_Count]   = @CurrentCount,
        [Last_Failed_Login_At] = @NowUtc,
        [Lockout_End_Utc]      = COALESCE(@NewLockoutEnd, [Lockout_End_Utc])
    WHERE [User_ID] = @UserId;

    SET @FailedLoginCount = @CurrentCount;
    SET @LockoutEndUtc    = COALESCE(@NewLockoutEnd, @ExistingLockoutEnd);

    -- ADDITIVE, and the ONLY change this procedure has had (DapperLayerPlan.md, Prompt 2).
    --
    -- The three OUTPUT parameters above are DELIBERATELY UNTOUCHED: anything still calling this the old
    -- way (a raw SqlCommand with three ParameterDirection.Output parameters) keeps working unchanged.
    -- What is new is the SELECT below, which re-emits exactly the same three values as a RESULT SET,
    -- because Dapper can only read OUTPUT parameters through DynamicParameters — the untyped, string-keyed
    -- plumbing the Dapper migration exists to delete. A result set maps onto CRC.Data/Models/
    -- FailedLoginResult.cs by name, with the compiler checking the shape.
    --
    -- 🔴 THE TWO RETURN STATEMENTS ABOVE SKIP THIS SELECT, and that is why SqlData reads it with
    -- QuerySingleOrDefaultAsync rather than QuerySingleAsync. An unknown @Username and an already-active
    -- lockout window each RETURN early, emitting NO result set at all. Neither is reachable from
    -- AccountController.Login — it only calls this after spUsers_ValidateLogin returned a row and after
    -- its own lockout check has passed — but a caller must still handle "no row". See CoreFlow.md §5.
    SELECT @LockoutTriggered AS [LockoutTriggered],
           @LockoutEndUtc    AS [LockoutEndUtc],
           @FailedLoginCount AS [FailedLoginCount];
END;
GO
