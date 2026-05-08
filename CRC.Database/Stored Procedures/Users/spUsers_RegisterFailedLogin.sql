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
END;
GO
