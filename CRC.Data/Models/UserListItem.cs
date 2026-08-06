namespace CRC.Data.Models
{
    // A dbo.Users row for the Admin > Users table — returned by spUsers_GetAll, ordered by User_ID DESC
    // (newest account first; the caller must not re-sort).
    //
    // 🔴 IT DOES NOT CARRY A PASSWORD HASH, and that is the point of it being a separate type rather than
    // a reuse of UserAuthRecord. spUsers_GetAll is the only one of the three read procedures that omits
    // Password_Hash, and this endpoint's JSON is the one that reaches a browser. A hash that is not in the
    // model cannot be leaked by a careless `Ok(users)`.
    //
    // TWO NAMING TRAPS, both real, both found by reading the .sql rather than by assuming:
    //
    //   • Staff_ID is NOT aliased here, though spUsers_ValidateLogin and spUsers_GetById both alias it to
    //     StaffId. Three procedures over one column, two spellings. Dapper maps by name, so the property
    //     below must be Staff_ID or it silently stays null.
    //   • FailedLoginCount, LastFailedLoginAt and LockoutEndUtc ARE aliased (from Failed_Login_Count,
    //     Last_Failed_Login_At, Lockout_End_Utc) — so within one result set, some columns are underscored
    //     and some are camel-ish. Neither convention wins; the procedure is the contract.
    public class UserListItem
    {
        public int User_ID { get; set; }
        public string User_Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string User_Email { get; set; } = string.Empty;

        // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF. The Web layer turns this into both `userType` (the number)
        // and `userTypeName` (the label) — see CoreFlow.md §4 for the JSON.
        public string? Staff_ID { get; set; }
        public int User_Type { get; set; }

        // DATETIME NOT NULL in dbo.Users; nullable here so a NULL cannot become a 500 (see UserAuthRecord).
        public DateTime? Created_At { get; set; }
        public DateTime? Last_Login { get; set; }

        // The lockout state. Failed_Login_Count is INT NOT NULL DEFAULT 0; the other two are NULL until the
        // account first fails a login. "Is this account locked?" is NOT a column — it is
        // LockoutEndUtc > UtcNow, computed by the caller, which is why an expired lockout window still
        // leaves a non-null LockoutEndUtc sitting in the row until the next successful login clears it.
        //
        // These arrive as DATETIME with Kind = Unspecified and are UTC; the caller must SpecifyKind before
        // comparing or formatting.
        public int? FailedLoginCount { get; set; }
        public DateTime? LastFailedLoginAt { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
    }
}
