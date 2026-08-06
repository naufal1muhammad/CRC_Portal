namespace CRC.Data.Models
{
    // What spUsers_RegisterFailedLogin decided about one failed login attempt: the running attempt count,
    // and whether THIS attempt is the one that locked the account.
    //
    // 🔴 THIS TYPE EXISTS BECAUSE OF A .sql CHANGE, and it is the only such change in the whole Dapper
    // migration. The procedure returns its answer through three OUTPUT parameters (@LockoutTriggered BIT,
    // @LockoutEndUtc DATETIME, @FailedLoginCount INT), and Dapper can only read an OUTPUT parameter through
    // DynamicParameters — a string-keyed bag with a manual `.Get<T>("@Name")` per value, which is exactly
    // the untyped plumbing this migration exists to delete. So Prompt 2 appended a trailing
    // `SELECT @LockoutTriggered AS …, @LockoutEndUtc AS …, @FailedLoginCount AS …` to the procedure and
    // KEPT ALL THREE OUTPUT PARAMETERS, so that the change is invisible to anything still calling it the
    // old way. The three properties below are named for that SELECT's column aliases. See CoreFlow.md §5.
    //
    // ../HEART/HEART.DataAccess/Models/FailedLoginResult.cs is the same idea in the sibling portal, but
    // NOT the same shape — HEART's has two properties (FailedLoginCount, LockoutUntilUtc) and infers "did
    // this attempt trigger the lockout" from the expiry being non-null. nucentra's procedure answers that
    // question itself, in a BIT, and the two answers are not interchangeable: LockoutEndUtc is also
    // non-null on an attempt against an ALREADY-locked account, which did not trigger anything.
    public class FailedLoginResult
    {
        // True when the account is locked as of this attempt — either because the attempt reached
        // MaxFailedAttempts, or because a lockout window set by an earlier attempt is still open.
        public bool LockoutTriggered { get; set; }

        // When the lockout expires, UTC, or null when the account is not locked. Arrives as a DATETIME
        // with Kind = Unspecified; SpecifyKind it before comparing with DateTime.UtcNow.
        public DateTime? LockoutEndUtc { get; set; }

        // The failed-attempt count AFTER this attempt. Resets to 1 when the previous failure is older than
        // AttemptWindowMinutes — the count is "failures within the window", not "failures ever".
        public int FailedLoginCount { get; set; }
    }
}
