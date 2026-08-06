using CRC.Data.Models;

namespace CRC.Data.Data
{
    // =================================================================================================
    // The data-access contract for nucentra. Every stored procedure in CRC.Database gets exactly one
    // method here, and SqlData is its only implementation. A controller depends on this interface and
    // never on DatabaseHelper, SqlParameter, SqlConnection or DataTable.
    //
    // This file is the DOCUMENTATION of the data layer; SqlData.cs is the mechanism. Read this one to
    // find out what the portal can ask the database. Read that one only when you need to know how.
    //
    // -------------------------------------------------------------------------------------------------
    // THE RULES — every prompt in DapperLayerPlan.md obeys these, and so does every later change
    // -------------------------------------------------------------------------------------------------
    //
    // 1. ONE METHOD PER STORED PROCEDURE. No method calls two procedures. The two transactional units of
    //    work (Prompts 3 and 6) are the only exceptions, and each is named and commented as such where it
    //    is declared — a reader must never have to guess whether a method is atomic.
    //
    // 2. NAME THE METHOD FOR WHAT IT DOES, NOT FOR THE PROCEDURE. GetActiveBranchesAsync, not
    //    SpBranchListActiveAsync. The procedure name belongs in the comment above it and in the Dapper
    //    call inside SqlData; a caller reading the interface should not need to know it exists.
    //
    // 3. EVERY METHOD CARRIES A `//` COMMENT saying what it is for and naming the procedure it calls.
    //    Anything surprising about the procedure — a column that is nullable when it looks like it should
    //    not be, a result set that is empty rather than throwing, an ordering the UI depends on — goes in
    //    that comment. That surprise is the most valuable thing in this file.
    //
    // 4. GROUP BY FEATURE AREA under a `// ----- Area (where it is used) -----` banner, and keep the
    //    banners AND the methods inside them in THE SAME ORDER AS SqlData.cs. The two files are meant to
    //    be read side by side; a divergent order makes that impossible and nothing will warn you.
    //
    // 5. RETURN List<T>, T? OR A SCALAR. Never DataTable, never object, never dynamic. A row that may not
    //    exist returns T? and the caller decides what "not found" means. The whole point of this layer is
    //    that a mistyped column name becomes a compile-time or startup-time problem instead of a page a
    //    user hits at 4pm.
    //
    // 6. MODELS LIVE IN CRC.Data/Models/, one POCO per file, named for the data and not for the
    //    procedure. Reuse an existing model when a procedure genuinely returns the same shape; do NOT add
    //    a column alias to a .sql file to force a fit.
    //
    // -------------------------------------------------------------------------------------------------
    // 🔴 THE @User_ID RULE — the one thing in this migration that fails silently
    // -------------------------------------------------------------------------------------------------
    //
    // 24 procedures declare a parameter called @User_ID, and it means TWO DIFFERENT THINGS. The tell is
    // the default, and getting it wrong is a data-integrity bug that breaks no build and no page.
    //
    // ── `@User_ID INT = NULL` — THE ACTOR (19 procedures) ────────────────────────────────────────────
    //
    // "Who is performing this write", recorded on the dbo.AuditTrails row the procedure inserts. It is
    // NOT a business argument, so it NEVER APPEARS IN A METHOD SIGNATURE IN THIS FILE. SqlData supplies
    // it from DatabaseHelper.CurrentUserId and puts a comment on every one of the 19 calls saying so.
    //
    //     spBranch_Insert              spPatientAppointment_Insert       spStaff_Insert
    //     spBranch_Update              spPatientAppointment_Update       spStaff_Update
    //     spBranch_Delete              spPatientAppointment_Delete       spStaff_Delete
    //     spPatientBasic_Insert        spPatientAppointment_UpdateStatus spStaffDocument_Insert
    //     spPatientBasic_Update        spPatientDocument_Insert          spStaffDocument_Delete
    //     spPatient_DeleteCascade      spPatientDocument_Delete          spStaffSlots_CreateRange
    //                                                                    spStaffSlots_Delete
    //
    // DatabaseHelper used to add this parameter by itself: before running any command it queried
    // sys.parameters, asked "does this procedure declare @User_ID?", and appended the claim value if so.
    // Dapper has no equivalent hook — it sends the anonymous object's properties and nothing else. And
    // because all 19 declare a DEFAULT, a forgotten parameter does not throw. It writes
    // AuditTrails.User_Id = 0, and nobody finds out until somebody needs the audit trail.
    //
    // ── `@User_ID INT` (no default) — A TARGET USER ROW (5 procedures) ───────────────────────────────
    //
    // "Which user row this operates ON". This IS an ordinary argument and DOES appear in the method
    // signature; the caller decides its value and it has nothing to do with who is logged in.
    //
    //     spUsers_GetById        spUsers_Unlock          spUsers_UpdatePassword
    //     spUsers_ResetFailedLogins                      spUsers_UpdateLastLogin
    //
    // spUsers_Unlock is the one to stare at. Its @User_ID is THE LOCKED-OUT ACCOUNT BEING UNLOCKED — a
    // SUPERUSER unlocking somebody else. Filling it from the caller's claim would unlock the
    // administrator's own account, leave the locked-out user locked, and report success.
    //
    // That asymmetry is exactly why the injection is not hidden inside a generic helper: each call site
    // in SqlData makes the choice explicitly, in the open, where a reader can see which kind it is.
    //
    // (spPatientDocument_GetById LOOKS like it declares @User_ID and does not — its header comment says
    // "Read-only: no @User_ID and no audit row", and a naive grep matches the comment. Trust the two
    // lists above, not a grep.)
    //
    // See DapperLayerPlan.md's "@User_ID" section and CoreFlow.md §0.
    // =================================================================================================
    public interface IDatabaseData
    {
        // Methods are added here by Prompts 1-9, one feature area at a time, under the banners described
        // in rule 4.

        // ----- Lookups (LU_* reference data) -----
        //
        // Fourteen procedures, every one a plain read with no @User_ID and no audit row. Eleven return a
        // VARCHAR code and its display name and come back as LookupItem; the three spLU_LOCATION_* ones
        // return the INT-keyed location tree and come back as LocationLookupItem. Read LookupItem.cs for
        // why there are two models and not fourteen, and SqlData.QueryLookupAsync for why the eleven are
        // mapped by column ORDINAL rather than by name — the short version is that each procedure names
        // its columns after its own table (Race_ID, Source_ID, PjAppType_ID …) and no .sql may be aliased.
        //
        // NONE of the fourteen filters on anything but its parent id, and none takes an "active" flag:
        // nucentra's reference tables have no IsActive column, so every row in the table reaches the
        // dropdown. Each procedure's ORDER BY is part of its contract — a caller must not re-sort.

        // Discharge reasons for the discharge dialog (NORMAL, BENIGN POLYPS, PRECANCEROUS POLYPS,
        // CANCER). A NULL DischargeType_ID on PatientBasic is the definition of an active patient.
        // Calls spLU_DischargeType_List; ordered by name.
        Task<List<LookupItem>> GetDischargeTypesAsync();

        // Marital statuses for the patient demographics form. Calls spLU_MaritalStatus_List; ordered by name.
        Task<List<LookupItem>> GetMaritalStatusesAsync();

        // Occupations for the patient demographics form. Calls spLU_Occupation_List; ordered by name.
        Task<List<LookupItem>> GetOccupationsAsync();

        // The organizations a branch can belong to. Calls spLU_ORGANIZATION_List; ordered by name.
        // Branch stores BOTH Organization_ID and Organization_Name (denormalized), so a caller saving a
        // branch sends the pair from this list rather than the id alone.
        Task<List<LookupItem>> GetOrganizationsAsync();

        // Patient document categories (identification, referral letter, iFOBT result, consent form).
        // Calls spLU_PatientDocumentType_List; ordered by name.
        Task<List<LookupItem>> GetPatientDocumentTypesAsync();

        // The four patient-journey step types — PATIENT ASSESSMENT, COLONOSCOPY, FOLLOW UP, SURVEILLANCE.
        // Calls spLU_PJ_AppType_List. NOTE the ordering: this is the one lookup ordered by ID rather than
        // by name, because the ids are sequenced in clinical order and alphabetical would scramble them.
        Task<List<LookupItem>> GetJourneyAppointmentTypesAsync();

        // Races for the patient demographics form. Calls spLU_Race_List; ordered by name.
        Task<List<LookupItem>> GetRacesAsync();

        // Religions for the patient demographics form. Calls spLU_Religion_List; ordered by name.
        Task<List<LookupItem>> GetReligionsAsync();

        // The nine routes a patient reaches the centre by (walk-in, GP, hospital referral, corporate,
        // online …). Calls spLU_Source_List; ordered by name.
        Task<List<LookupItem>> GetSourcesAsync();

        // Staff document categories (CV, certificates). Calls spLU_STAFFDOCUMENTTYPE_List; ordered by name.
        Task<List<LookupItem>> GetStaffDocumentTypesAsync();

        // Staff types — endoscopist, registered nurse, anaesthesia provider, endoscopy technician,
        // gastrointestinal assistant. Calls spLU_STAFFTYPE_List; ordered by name. This is the lookup whose
        // codes are three-letter mnemonics ("ANE", "END", "NUR") rather than "01"/"02".
        Task<List<LookupItem>> GetStaffTypesAsync();

        // The states of Malaysia — level 1 of the dbo.LU_LOCATION tree (LocationType = 1). Calls
        // spLU_LOCATION_ListStates. Returns LocationId and Name; ParentId is not selected (a state has no
        // parent) and stays null on every row.
        Task<List<LocationLookupItem>> GetStatesAsync();

        // The cities of one state — level 2 (LocationType = 2, ParentId = stateId). Calls
        // spLU_LOCATION_ListCityByState. An unknown or empty stateId is NOT an error: the procedure
        // returns an empty set, so the caller decides what "no cities" means.
        Task<List<LocationLookupItem>> GetCitiesByStateAsync(int stateId);

        // The postcodes of one city — level 3 (LocationType = 3, ParentId = cityId). Calls
        // spLU_LOCATION_ListPostcodesByCity. Same empty-set-not-error behaviour as the cities above; note
        // that a postcode's "Name" IS the postcode ("82100"), which is why it is text and not a number.
        Task<List<LocationLookupItem>> GetPostcodesByCityAsync(int cityId);

        // ----- Branch (Admin > Branch) -----
        //
        // Six procedures, three of which declare `@User_ID INT = NULL` — spBranch_Insert, spBranch_Update
        // and spBranch_Delete. That is THE ACTOR for the dbo.AuditTrails row each one writes, so it is
        // absent from the three signatures below: SqlData supplies it from DatabaseHelper.CurrentUserId.
        // The three reads declare no @User_ID and write no audit row.

        // Every branch, active or not, for the Admin > Branch table. Calls spBranch_ListAll; ordered by
        // branch name.
        Task<List<BranchDetail>> GetAllBranchesAsync();

        // One branch for the edit dialog; null when no branch has that id. Calls spBranch_GetById, which
        // returns exactly the same seven columns as spBranch_ListAll — hence one model for both.
        Task<BranchDetail?> GetBranchByIdAsync(string branchId);

        // Active branches only (Branch_Status = 1) as {id, name, state}, for the branch dropdowns on the
        // staff and appointment forms. Calls spBranch_ListActive; ordered by branch name.
        Task<List<BranchOption>> GetActiveBranchesAsync();

        // Creates a branch and RETURNS ITS NEW ID, which the caller cannot predict: spBranch_Insert
        // generates Branch_ID itself as {Organization_ID}{4-digit LU_LOCATION state id}{3-digit sequence}
        // and ends with `SELECT @Branch_ID AS NewBranch_ID`. It RAISERRORs — surfacing as a SqlException —
        // when Organization_ID is blank, when Branch_State is blank, or when Branch_State does not match a
        // LocationType = 1 row in LU_LOCATION by NAME. Writes an INSERT row to dbo.AuditTrails.
        Task<string> CreateBranchAsync(string branchName, string branchLocation, string branchState,
            bool branchStatus, string organizationId, string organizationName);

        // Updates every field of a branch except its id. Calls spBranch_Update, which is SILENT when the
        // id matches nothing: no error, no audit row, no way to tell from here. It also does NOT re-check
        // Branch_State against LU_LOCATION the way the insert does — the validation is asymmetric.
        Task UpdateBranchAsync(string branchId, string branchName, string branchLocation, string branchState,
            bool branchStatus, string organizationId, string organizationName);

        // Deletes a branch. Calls spBranch_Delete, which — like the update — is silent when the id matches
        // nothing, and which audits only when a row actually went. There is NO referential check anywhere:
        // dbo.Branch has no incoming foreign key, and both Staff.Staff_Base and PatientAppointment.Branch_ID
        // are plain VARCHAR(100) columns holding a Branch_ID. Deleting a branch that staff are based at, or
        // that appointments are booked into, succeeds and orphans them.
        Task DeleteBranchAsync(string branchId);

        // ----- Users, authentication and lockout -----
        //
        // Nine procedures, all over dbo.Users, and the whole of nucentra's authentication. Read CoreFlow.md
        // §2 before changing any of them.
        //
        // 🔴 FIVE OF THE NINE DECLARE @User_ID, AND IN ALL FIVE IT IS A TARGET — spUsers_GetById,
        // spUsers_Unlock, spUsers_UpdatePassword, spUsers_ResetFailedLogins and spUsers_UpdateLastLogin. It
        // names THE USER ROW BEING READ OR WRITTEN, not the person doing it. THE TELL IS THE DEFAULT: these
        // five declare `@User_ID INT` with none, whereas all 19 audit-actor procedures elsewhere declare
        // `@User_ID INT = NULL`. So the five take an ordinary `int userId` argument below and NONE of them
        // is given DatabaseHelper.CurrentUserId.
        //
        // spUsers_Unlock is the one to stare at: filling its @User_ID from the caller's claim would unlock
        // the SUPERUSER's own account, leave the locked-out user locked, and return "Account unlocked."
        //
        // NONE of the nine writes a dbo.AuditTrails row — unusual for procedures that write. The security
        // trail for login, lockout, unlock and logout is the Serilog audit channel instead
        // (AuditLog.* → Logs/audit-*.log), written by AccountController. See CoreFlow.md §9.

        // The dbo.Users row for a username, for the login path — identity, password hash and lockout
        // state. Null when no account has that username. Calls spUsers_ValidateLogin.
        //
        // 🔴 THE PROCEDURE VALIDATES NOTHING despite its name: it is a plain SELECT by username, with no
        // password check, no lockout enforcement and no side effect. A non-null return means "this username
        // exists", NOT "this login succeeded". The password comparison (PasswordHasher<string>.
        // VerifyHashedPassword) and the lockout-window comparison both happen in AccountController.Login.
        Task<UserAuthRecord?> GetUserForLoginAsync(string username);

        // One user by id, for the Change Password page and for naming the account being unlocked. Null when
        // no user has that id. Calls spUsers_GetById — whose @User_ID is A TARGET (the row to read), not
        // the caller.
        //
        // It selects NINE columns, three fewer than spUsers_ValidateLogin: THE LOCKOUT STATE IS NOT AMONG
        // THEM. That is why this returns UserAccountRecord and not UserAuthRecord — a lockout decision made
        // from this result would read 0 and null on a locked account, silently. Use GetUserForLoginAsync
        // when you need to know whether someone is locked.
        Task<UserAccountRecord?> GetUserByIdAsync(int userId);

        // Every user account for the Admin > Users table. Calls spUsers_GetAll; ordered by User_ID
        // DESCENDING — newest account first, and the caller must not re-sort. This is the only one of the
        // three read procedures that does NOT select Password_Hash, which is why it gets its own model.
        Task<List<UserListItem>> GetAllUsersAsync();

        // Creates a user account. Calls spUsers_Register, which RAISERRORs — surfacing as a SqlException —
        // on four separate conditions, none of which this method can pre-empt:
        //   • the username already exists (dbo.Users has a UNIQUE index on Username);
        //   • userType is 3 (STAFF) and staffId is blank;
        //   • staffId does not match a row in dbo.Staff;
        //   • staffId is ALREADY linked to another user account — one staff member, at most one login.
        // The caller passes a hash, never a password: hash it with PasswordHasher<string> first.
        // Created_At and Last_Login are both stamped GETUTCDATE() by the procedure, so a brand-new account
        // reads as though it had just logged in.
        Task RegisterUserAsync(string userName, string username, string userEmail, string passwordHash,
            int userType, string? staffId);

        // Records one failed login attempt and returns the resulting lockout state. Calls
        // spUsers_RegisterFailedLogin — the ONLY procedure in nucentra with OUTPUT parameters, and the only
        // .sql file the Dapper migration edited (additively: a trailing SELECT of the same three values,
        // with all three OUTPUT parameters kept). See FailedLoginResult for why.
        //
        // The thresholds are passed in per call from IOptions<LoginLockoutOptions> (appsettings.json
        // "Account:LoginLockout"), not read from the database — the procedure holds no policy of its own.
        //
        // Returns null when the procedure emitted no result set, which it does on its two early-RETURN
        // paths: an unknown username, and an attempt against an account whose lockout window is already
        // open. Neither is reachable from AccountController.Login, which calls this only after
        // GetUserForLoginAsync returned a row and after its own lockout check has passed.
        Task<FailedLoginResult?> RegisterFailedLoginAsync(string username, int maxFailedAttempts,
            int lockoutMinutes, int attemptWindowMinutes);

        // Clears Failed_Login_Count, Last_Failed_Login_At and Lockout_End_Utc after a SUCCESSFUL login.
        // Calls spUsers_ResetFailedLogins, whose @User_ID is A TARGET — the user who just logged in, which
        // the caller passes explicitly. Silent when the id matches nothing.
        Task ResetFailedLoginsAsync(int userId);

        // Clears the same three columns on behalf of somebody else — a SUPERUSER unlocking a locked-out
        // account. Calls spUsers_Unlock.
        //
        // 🔴 @User_ID HERE IS THE LOCKED-OUT ACCOUNT, NOT THE SUPERUSER PERFORMING THE UNLOCK. This is the
        // single call in the whole migration where confusing the ACTOR with the TARGET would unlock the
        // wrong account and report success. Unlike spUsers_ResetFailedLogins it is NOT silent on a bad id:
        // it RAISERRORs "User not found." (severity 16 → a SqlException).
        Task UnlockUserAsync(int userId);

        // Stamps dbo.Users.Last_Login with GETUTCDATE(). Calls spUsers_UpdateLastLogin, whose @User_ID is A
        // TARGET — the user who just logged in. Silent when the id matches nothing.
        Task UpdateLastLoginAsync(int userId);

        // Replaces dbo.Users.Password_Hash. Calls spUsers_UpdatePassword, whose @User_ID is A TARGET — and
        // which RAISERRORs "User not found." rather than failing silently.
        //
        // The caller passes a HASH, never a password. The procedure verifies nothing: the current-password
        // check, the "must differ from the current one" rule and the whole password policy live in
        // AccountController.ChangePassword. There is no password history and no MustChangePassword column,
        // so nothing forces the seeded SUPERUSER password to ever be changed (see SEEDING.md).
        Task UpdateUserPasswordAsync(int userId, string passwordHash);
    }
}
