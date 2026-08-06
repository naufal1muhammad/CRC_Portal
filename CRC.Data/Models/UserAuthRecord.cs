namespace CRC.Data.Models
{
    // The dbo.Users row the LOGIN PATH reads: identity, the stored password hash, and the lockout state.
    // Returned by spUsers_ValidateLogin, which is the only procedure that selects the three lockout
    // columns — hence a separate type from UserAccountRecord (spUsers_GetById), whose result set genuinely
    // does not contain them. See UserAccountRecord for the longer version of that argument.
    //
    // 🔴 THE PROCEDURE'S NAME IS A LIE, AND IT MATTERS. spUsers_ValidateLogin VALIDATES NOTHING. It is a
    // plain `SELECT TOP 1 … WHERE Username = @Username` — no password comparison, no lockout enforcement,
    // no side effect. Every decision is made in C#, in AccountController.Login: the lockout window is
    // compared against DateTime.UtcNow there, and the password is verified there with
    // PasswordHasher<string>.VerifyHashedPassword. A caller that treats a returned row as "the login
    // succeeded" has authenticated nobody.
    public class UserAuthRecord
    {
        public int User_ID { get; set; }
        public string User_Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string User_Email { get; set; } = string.Empty;

        // 🔴 A PBKDF2 HASH produced by Microsoft.AspNetCore.Identity.PasswordHasher<string> (dbo.Users.
        // Password_Hash, VARCHAR(500), aliased to PasswordHash by the procedure). It is NEVER a plaintext
        // password, it can never be turned back into one, and the only correct thing to do with it is pass
        // it to VerifyHashedPassword. DO NOT LOG IT, do not put it in an audit line, do not return it to a
        // browser, and do not include it in any JSON an endpoint emits. The hash is salted per row, so two
        // users with the same password have different values here — comparing two of these for equality
        // answers nothing.
        public string PasswordHash { get; set; } = string.Empty;

        // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF. Becomes the "UserType" claim, which is what all five
        // authorization policies check. See CoreFlow.md §2.
        public int User_Type { get; set; }

        // dbo.Users.Staff_ID, aliased to StaffId by the procedure. NULL for everyone who is not a STAFF
        // user — only User_Type = 3 requires one (spUsers_Register enforces that, and enforces that a
        // Staff_ID is linked to at most one account).
        public string? StaffId { get; set; }

        // Both columns are DATETIME NOT NULL in dbo.Users, so neither can arrive null from a row this
        // schema produced. They are nullable here for the same reason BranchDetail.Branch_Status is:
        // Dapper THROWS mapping a NULL onto a non-nullable value type, which would turn a legacy or
        // hand-inserted row into a 500 instead of the empty string the endpoints have always shown.
        public DateTime? Created_At { get; set; }
        public DateTime? Last_Login { get; set; }

        // ── The lockout state. These three exist ONLY on this type, not on UserAccountRecord. ──────────
        //
        // Failed_Login_Count is INT NOT NULL DEFAULT 0; the other two are genuinely NULL-able and are NULL
        // for an account that has never failed a login. All three are written by spUsers_RegisterFailedLogin
        // and cleared by spUsers_ResetFailedLogins (on a successful login) and spUsers_Unlock (by a
        // SUPERUSER).
        //
        // LockoutEndUtc is UTC, but SQL Server hands back a DATETIME with Kind = Unspecified. The caller
        // must SpecifyKind(…, Utc) before comparing it with DateTime.UtcNow — AccountController.Login does,
        // and getting that wrong turns a 15-minute lockout into a lockout that is off by the server's UTC
        // offset in whichever direction hurts.
        public int? FailedLoginCount { get; set; }
        public DateTime? LastFailedLoginAt { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
    }
}
