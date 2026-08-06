namespace CRC.Data.Models
{
    // One dbo.Users row fetched BY ID — returned by spUsers_GetById, which the portal uses to load the
    // Change Password page and to name the account a SUPERUSER is about to unlock.
    //
    // 🔴 WHY THIS IS NOT UserAuthRecord, even though nine of its properties are identical.
    //
    // spUsers_GetById selects NINE columns; spUsers_ValidateLogin selects TWELVE. The three it leaves out
    // are exactly the lockout state — FailedLoginCount, LastFailedLoginAt, LockoutEndUtc. Sharing one type
    // between the two procedures would compile, run, and silently hand every caller of GetUserByIdAsync a
    // FailedLoginCount of 0 and a LockoutEndUtc of null on an account that is, in fact, locked — because
    // Dapper leaves a property alone when the result set has no column for it. "This user is not locked"
    // is precisely the wrong thing to be silently wrong about, and no exception or log line would say so.
    //
    // Two types make the compiler answer the question instead: a lockout decision can only be made from a
    // UserAuthRecord, because that is the only one that has the columns. That is the same reasoning
    // BranchDetail applies in reverse — spBranch_ListAll and spBranch_GetById DO return the same seven
    // columns, so they share one model. Reuse the shape, never the name.
    public class UserAccountRecord
    {
        public int User_ID { get; set; }
        public string User_Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string User_Email { get; set; } = string.Empty;

        // 🔴 A PBKDF2 HASH from Microsoft.AspNetCore.Identity.PasswordHasher<string> — see the identical
        // note on UserAuthRecord.PasswordHash. It is here because ChangePassword must verify the CURRENT
        // password before writing a new one, and it verifies against dbo.Users.Password_Hash rather than
        // against anything in the auth cookie. NEVER LOG IT, never return it to a client.
        public string PasswordHash { get; set; } = string.Empty;

        // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF. See CoreFlow.md §2.
        public int User_Type { get; set; }

        // dbo.Users.Staff_ID, aliased to StaffId by this procedure. NOTE that spUsers_GetAll does NOT alias
        // it and returns the raw Staff_ID — which is why UserListItem spells the property the other way.
        // Nothing enforces consistency between two procedures over the same table; read the .sql.
        public string? StaffId { get; set; }

        // DATETIME NOT NULL in dbo.Users; nullable here so a NULL cannot become a 500 (see UserAuthRecord).
        public DateTime? Created_At { get; set; }
        public DateTime? Last_Login { get; set; }
    }
}
