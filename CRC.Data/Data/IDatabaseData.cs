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

        // ----- Staff (Admin > Staff) -----
        //
        // Five procedures over dbo.Staff. THREE OF THEM DECLARE `@User_ID INT = NULL` — spStaff_Insert,
        // spStaff_Update and spStaff_Delete — which is THE ACTOR for the dbo.AuditTrails row each one
        // writes, so it is absent from every signature below and SqlData supplies it from
        // DatabaseHelper.CurrentUserId. spStaff_List and spStaff_GetById declare no @User_ID at all.
        //
        // A Staff_ID is a VARCHAR(100) STRING, not a number: spStaff_Insert composes it as
        // {Staff_Type}-{5-digit global sequence} — "END-00003", "NUR-00011" — so the prefix is the
        // LU_STAFFTYPE code (CoreFlow.md §3.4). Parsing one to an int loses everything.

        // Every staff member for the Admin > Staff table, seven columns, ordered by Staff_Name. Calls
        // spStaff_List, which LEFT JOINs LU_STAFFTYPE — so StaffType_Name is null for a staff member whose
        // Staff_Type no longer matches a lookup row, and nothing prevents that (there is no foreign key).
        //
        // REUSED BY PROMPT 6: PatientController.GetAppointmentStaffList populates the appointment form's
        // staff dropdown from this same procedure. Do not add a second method for it.
        Task<List<StaffListItem>> GetAllStaffAsync();

        // One staff member with every column, for the Edit Staff form and the read-only My Profile page;
        // null when no staff member has that id. Calls spStaff_GetById (SELECT TOP 1).
        //
        // It returns ELEVEN COLUMNS MORE than spStaff_List — the birth date, age, gender, the whole
        // residential address and the base branch — which is why StaffDetail and StaffListItem are two
        // models and not one. See §5.3's spUsers_GetById note for what sharing a model across a strict
        // subset costs.
        Task<StaffDetail?> GetStaffByIdAsync(string staffId);

        // Creates a staff member and RETURNS THE NEW Staff_ID, which the caller cannot predict: calls
        // spStaff_Insert, which composes the id itself as {Staff_Type}-{RIGHT('00000' + sequence, 5)} and
        // ends with `SELECT @Staff_ID AS NewStaff_ID`. It RAISERRORs — a SqlException in C# — on a blank
        // @Staff_Type, because a blank prefix would produce the id "-00007".
        //
        // 🔴 THE SEQUENCE IS GLOBAL AND IT REUSES NUMBERS, exactly like spBranch_Insert (§3.2):
        // MAX(TRY_CAST(RIGHT(Staff_ID, 5) AS INT)) scans the WHOLE table, so the third staff member overall
        // is "…-00003" whatever their type, and deleting the newest one makes the next insert re-issue its
        // number. A different type then yields a genuinely new id; the SAME type collides on the primary
        // key and the insert fails. See CoreFlow.md §3.4.
        //
        // This is the NON-transactional path, used by POST /Staff/SaveStaff, which saves a staff member on
        // its own with no documents. SaveStaffWithDocumentsAsync runs the same procedure inside its
        // transaction instead — both go through one private helper in SqlData so the parameter list cannot
        // drift between them.
        Task<string> CreateStaffAsync(StaffSaveInput staff);

        // Updates every field of a staff member except their id. Calls spStaff_Update, which is SILENT when
        // the id matches nothing — no error, no audit row, and no row count returned, so this method cannot
        // tell a real write from a missed one. It re-writes Staff_Type as well, so a staff member's type
        // can change while their Staff_ID keeps the OLD type's prefix; the id is never regenerated.
        Task UpdateStaffAsync(StaffSaveInput staff);

        // Deletes a staff member and everything that hangs off them, and tells the caller what happened.
        // Calls spStaff_Delete — READ StaffDeleteResult BEFORE CALLING THIS, because the procedure is
        // neither a plain delete nor a plain success/failure:
        //
        //   • It REFUSES to delete a staff member still referenced by dbo.PatientAppointment,
        //     dbo.PatientJourney or dbo.PatientJourneyAudit, returning Status = "Blocked" and a Message
        //     naming which of the three. That is the only referential protection dbo.Staff has — there are
        //     no foreign keys — and it is enforced in the procedure, so a direct DELETE bypasses it.
        //   • When it does delete, it CASCADES BY HAND to dbo.StaffSlots, dbo.StaffDocument AND dbo.Users,
        //     inside a transaction of its own. Deleting a staff member therefore deletes their LOGIN.
        //   • It returns TWO result sets, always, and the second carries the blob keys of the documents it
        //     just removed rows for. Storage is outside the transaction, so the caller deletes those
        //     objects afterwards. See StaffDeleteResult.
        Task<StaffDeleteResult> DeleteStaffAsync(string staffId);

        // 🔴 THE FIRST OF EXACTLY TWO TRANSACTIONAL UNITS OF WORK IN THIS FILE, and one of only two methods
        // in nucentra's data layer that calls more than one stored procedure. (The other is
        // SaveAppointmentAsync, added in Prompt 6.) Everything else here is one method, one procedure.
        //
        // Saves a staff member and their document changes ATOMICALLY: spStaff_Insert or spStaff_Update,
        // then spStaffDocument_GetById + spStaffDocument_Delete for each document being removed, then
        // spStaffDocument_Insert for each document being added — all on one connection, inside one
        // SqlTransaction, committed together or rolled back together. It throws on any failure; a throw
        // means the transaction was rolled back and NOTHING was written.
        //
        // WHY IT IS ATOMIC AT ALL: the mandatory-document rule (CoreFlow.md §4.4) says an ENDOSCOPIST is
        // not a valid record without a CV and an MMC certificate. A staff row that committed while its
        // documents did not would be exactly the state the rule exists to prevent, and no screen would ever
        // show that it had happened.
        //
        // ── uploadDocumentsAsync: why a callback, and why the blob writes are not hoisted out ───────────
        //
        // The caller passes a delegate that UPLOADS THE FILES AND RETURNS THE ROWS TO INSERT, and this
        // method invokes it after the staff row is written and before the document rows are. That is not
        // indirection for its own sake — it is the only ordering the blob key permits. The key is
        // staff/{Staff_ID}/{guid}{ext}, and for a NEW staff member the Staff_ID does not exist until
        // spStaff_Insert has run, inside this transaction. So "upload everything first, then call the data
        // layer" is impossible for an insert without moving id generation out of the procedure that owns
        // it.
        //
        // The delegate keeps IDocumentStorage — a CRC.Web service — on the CRC.Web side of the boundary.
        // CRC.Data has no reference to CRC.Web and must not gain one; a Func<> is a BCL type and carries no
        // dependency. What it costs is that a blob can be written and the transaction can still fail
        // afterwards, so the guarantee is kept by COMPENSATION rather than by ordering: the caller records
        // every key it writes and deletes them all if this method throws. See CoreFlow.md §6.6.
        //
        // The AUDIT LINES ARE THE CALLER'S JOB AND ARE WRITTEN AFTER THIS RETURNS, for the same reason —
        // an AuditLog line claiming a write that a rollback then erased is worse than no line at all. The
        // dbo.AuditTrails rows are different: the procedures write those themselves, inside the
        // transaction, so a rollback takes them with it. Both trails end up honest, by opposite means.
        Task<StaffSaveResult> SaveStaffWithDocumentsAsync(
            StaffSaveInput staff,
            IReadOnlyList<int> deleteDocumentIds,
            Func<string, Task<IReadOnlyList<StaffDocumentInput>>> uploadDocumentsAsync);

        // ----- Staff documents -----
        //
        // Six procedures over dbo.StaffDocument plus one over dbo.StaffDocumentSettings. TWO DECLARE
        // `@User_ID INT = NULL` — spStaffDocument_Insert and spStaffDocument_Delete — the ACTOR, supplied
        // by SqlData. The four reads declare none.
        //
        // Note that spStaffDocument_Insert has NO standalone method here: the only two callers are
        // StaffController.UploadStaffDocuments and the transactional save above, and the former is folded
        // into AddStaffDocumentAsync below while the latter runs it inside its own transaction.

        // Every document belonging to one staff member, newest first (UploadedOn DESC, then
        // StaffDocument_ID DESC — the tiebreak matters, because a batch uploaded in one request shares a
        // timestamp to the second). Calls spStaffDocument_List.
        //
        // 🔴 THE RESULT CARRIES BlobName. The endpoint that lists documents to a browser must not project
        // it — the container is private and the key is a server-side detail. See StaffDocumentItem.
        //
        // @Staff_ID is OPTIONAL in the procedure (`= NULL`, and a blank string is treated the same way), in
        // which case it returns EVERY document in the system. Nothing in the portal calls it that way
        // today; StaffController rejects a blank staffId before it gets here.
        Task<List<StaffDocumentItem>> GetStaffDocumentsAsync(string staffId);

        // One document by its identity; null when no row has that id. Calls spStaffDocument_GetById, which
        // selects exactly the same nine columns as spStaffDocument_List — hence one model for both.
        //
        // This is how the blob key is resolved for a download (to mint a five-minute read SAS) and for a
        // delete (to know what to remove from the container afterwards).
        //
        // REUSED BY PROMPT 8: DocumentsController's search page opens staff documents through the same
        // procedure. Do not add a second method for it.
        Task<StaffDocumentItem?> GetStaffDocumentByIdAsync(int documentId);

        // Records one uploaded document. Calls spStaffDocument_Insert, which stamps UploadedOn itself as
        // MALAYSIAN LOCAL TIME (`GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time'`),
        // not UTC — the one timestamp in nucentra that is not.
        //
        // The bytes must already be in the container: this writes the row that points at them and nothing
        // else. It writes an INSERT row to dbo.AuditTrails but DOES NOT RETURN THE NEW StaffDocument_ID —
        // there is no trailing SELECT — which is why every AuditLog.StaffDocumentUploaded line in the
        // portal records a document id of 0 and identifies the row by its blob key instead.
        Task AddStaffDocumentAsync(string staffId, string staffName, string documentTypeId,
            string documentTypeName, string fileName, string blobName, string contentType);

        // Deletes one document row and RETURNS THE BLOB KEY it pointed at, so the caller can remove the
        // object from storage. Null means no row was deleted and there is nothing to remove.
        //
        // 🔴 spStaffDocument_Delete IS THE SECOND PROCEDURE IN NUCENTRA WITH AN OUTPUT PARAMETER, and the
        // only one this layer still reads as one. (§5.3 calls spUsers_RegisterFailedLogin "the only" one;
        // it is not — that claim was made before this area was read. The difference is that this one's
        // OUTPUT is its whole answer and the procedure has no result set to append to without a .sql
        // change, which Prompt 3 was not permitted to make and did not need.) SqlData reads
        // @DeletedBlobName through DynamicParameters, in the one place in the file that does so.
        Task<string?> DeleteStaffDocumentAsync(int documentId);

        // Which document types matter for a staff type, for the mandatory-document rule and for Prompt 8's
        // Settings screen. Calls spStaffDocumentSettings_GetByStaffType.
        //
        // 🔴 IT RETURNS EVERY DOCUMENT TYPE, NOT ONLY THE MANDATORY ONES — all eight rows of
        // LU_STAFFDOCUMENTTYPE, with IsMandatory = 1 on the ones configured for this staff type and 0 on
        // the rest. A caller that wants "the mandatory ones" must filter; the row count answers nothing.
        //
        // REUSED BY PROMPT 8: SettingsController renders the same list as a checklist.
        Task<List<StaffDocumentSetting>> GetStaffDocumentSettingsAsync(string staffTypeId);

        // The document-type filter for the Documents search page: every type that either EXISTS ON A STORED
        // DOCUMENT or is defined in LU_STAFFDOCUMENTTYPE, unioned. Calls spStaffDocument_LookupDocuments.
        //
        // The union is the point, and it is a deliberate difference from GetStaffDocumentTypesAsync (the
        // plain spLU_STAFFDOCUMENTTYPE_List read used by the upload form). A document uploaded under a type
        // that was later removed from the lookup must still be findable, so its raw StaffDocumentType_ID
        // appears in the filter with the id itself standing in for the missing name. An upload form must
        // NOT offer that type; a search filter must.
        //
        // NOTHING IN PROMPT 3 CALLS THIS — StaffController has no search page. It is wrapped now because
        // the plan's rule is that the prompt reaching a procedure first creates its method.
        // CALLED BY PROMPT 8: DocumentsController.
        Task<List<LookupItem>> GetStaffDocumentTypeFiltersAsync();

        // The distinct names of every staff member who owns at least one document, ordered by name — the
        // staff filter on the same Documents search page. Calls spStaffDocument_StaffNames.
        //
        // It returns NAMES, NOT IDS, and it is an INNER JOIN: a document whose Staff_ID matches no staff
        // row contributes nothing, and two staff members who happen to share a name collapse into one
        // entry. That is what the filter control wants (it filters on the displayed name) and it is why
        // this returns List<string> rather than a lookup pair.
        //
        // NOTHING IN PROMPT 3 CALLS THIS either. CALLED BY PROMPT 8: DocumentsController.
        Task<List<string>> GetStaffDocumentStaffNamesAsync();

        // ----- Staff slots (Staff Schedule) -----
        //
        // dbo.StaffSlots is the availability an administrator (or a clinician, for themselves) publishes in
        // advance: ONE ROW IS ONE HOUR of one staff member's day, unique per (Staff_ID, SlotDate,
        // SlotStartTime), and booking an appointment consumes one or more of them. See CoreFlow.md §3.7.
        //
        // 🔴 THE FOLDER HOLDS SIX PROCEDURES AND THIS BANNER WRAPS FOUR. spStaffSlots_AssignAppointment and
        // spStaffSlots_ClearAppointment ARE DELIBERATELY ABSENT, NOT FORGOTTEN. Neither has a caller of its
        // own: both are run only from inside PatientController.SaveAppointment's transaction, which stamps
        // an appointment id onto the slots it consumes and clears it off the ones it releases. Wrapping them
        // here as standalone methods would publish two ways to change a slot's booking state — one atomic
        // and one not — and the non-atomic one is exactly the race the transaction exists to prevent. They
        // belong to SaveAppointmentAsync, the second of this file's two transactional units of work, and
        // PROMPT 6 ADDS THEM THERE. Do not add them to this banner.
        //
        // TWO OF THE FOUR DECLARE `@User_ID INT = NULL` — spStaffSlots_CreateRange and spStaffSlots_Delete —
        // which is THE ACTOR for the dbo.AuditTrails row each one writes, so it is absent from both
        // signatures and SqlData supplies it from DatabaseHelper.CurrentUserId. spStaffSlots_List and
        // spStaffSlots_GetOwner declare no @User_ID and write no audit row.

        // The hours one staff member has published, optionally narrowed to a date range, ordered by date
        // then start time. Calls spStaffSlots_List. Both dates are optional and independent: pass null for
        // either and that end is unbounded. An unknown Staff_ID is not an error — it returns an empty list.
        //
        // 🔴 TWO CALLERS, AND THE SECOND ONE IS A CONCURRENCY CHECK. StaffScheduleController.List renders
        // the schedule grid; PROMPT 6's PatientController.SaveAppointment runs this SAME procedure INSIDE
        // the appointment transaction to decide whether the hours being booked are still free. Both need
        // the same five columns, so StaffSlotItem serves both — read it before changing anything here,
        // especially the note about why the two time columns are strings and why the appointment id is the
        // only nullable one.
        //
        // Prompt 6 will need this call to run on the transaction's own connection. Adding an overload that
        // takes a connection and a transaction is the intended way to do that; do NOT make it read outside
        // the transaction, because the read IS the availability check.
        Task<List<StaffSlotItem>> GetStaffSlotsAsync(string staffId, DateTime? fromDate, DateTime? toDate);

        // Opens every whole hour between startTime and endTime, on every day from fromDate to toDate
        // inclusive, for one staff member — and reports how many it created and how many were already open.
        // Calls spStaffSlots_CreateRange.
        //
        // IT IS IDEMPOTENT BY DESIGN: a MERGE … WHEN NOT MATCHED against the unique index means re-running a
        // range that is already open creates nothing, errors on nothing, and answers { 0, N }. See
        // StaffSlotCreateResult.
        //
        // THE PROCEDURE VALIDATES, AND IT THROWS RATHER THAN RETURNING A STATUS. Five `THROW 50001` guards —
        // a blank Staff_ID, ToDate before FromDate, a range longer than 31 days, EndTime at or before
        // StartTime, and a time that is not on the hour — all surface as a SqlException here. The controller
        // pre-validates the shape of its inputs but NOT those five rules, so this can and does throw on
        // input the controller accepted (a 40-day range is the easy one to hit).
        //
        // Writes ONE dbo.AuditTrails row per call — not one per slot — summarising the whole range.
        Task<StaffSlotCreateResult> CreateStaffSlotRangeAsync(string staffId, DateTime fromDate,
            DateTime toDate, TimeSpan startTime, TimeSpan endTime);

        // Removes one published hour. Calls spStaffSlots_Delete.
        //
        // 🔴 IT REFUSES TO DELETE A BOOKED SLOT, and it says so by THROWING: `THROW 50002, 'Cannot delete a
        // slot that is already taken.'` when PatientAppointment_ID is not null, and `THROW 50003, 'Slot not
        // found.'` when the delete matched nothing. Both arrive here as a SqlException — there is no status
        // code and no row count — so a caller that wants to distinguish "gone" from "refused" must catch it.
        // That is the only thing stopping a slot vanishing out from under an appointment: dbo.StaffSlots has
        // a foreign key to dbo.PatientAppointment, but the FK constrains the appointment's existence, not
        // the slot's.
        //
        // OWNERSHIP IS NOT CHECKED HERE. The procedure deletes any slot id it is given. Resolve the owner
        // with GetStaffSlotOwnerAsync first — see that method.
        Task DeleteStaffSlotAsync(int staffSlotId);

        // The Staff_ID that owns a slot, or null when no slot has that id. Calls spStaffSlots_GetOwner
        // (SELECT TOP 1 Staff_ID — an empty result set for an unknown id, not an error).
        //
        // 🔴 THIS EXISTS FOR ONE REASON AND IT IS A SECURITY ONE. StaffSlot_ID is a sequential IDENTITY, so
        // a STAFF user can guess every other clinician's slot ids by counting. StaffScheduleController.Delete
        // therefore resolves the owner SERVER-SIDE and runs it through User.CanAccessStaff() before calling
        // DeleteStaffSlotAsync — it never trusts a staff id from the request body, because the delete request
        // does not carry one and could not be believed if it did. Do not "simplify" the delete by dropping
        // this round trip.
        Task<string?> GetStaffSlotOwnerAsync(int staffSlotId);

        // ----- Staff performance -----

        // Everything the Staff Performance panel shows for one clinician, in one call. Calls
        // spStaff_GetPerformance.
        //
        // 🔴 IT RETURNS FOUR RESULT SETS AND THE ORDER IS THE CONTRACT — summary counts, then hours by
        // appointment type, then complications, then anomalies. (DapperLayerPlan.md's Prompt 4 says five;
        // the .sql, the deployed procedure and the controller all say four. The phantom fifth is the SELECT
        // inside grid 4's CTE.) Grids 3 and 4 are both {string, int} and are told apart by position alone,
        // so reading them out of order produces convincing, wrong output. See StaffPerformanceResult.
        //
        // The four grids answer four different questions from three different tables — journeys, attended
        // appointments, and colonoscopies — so an empty list is "none recorded", never "something failed".
        // An unknown Staff_ID is not an error: grid 1 comes back as one row of NULLs (hence the nullable
        // ints) and grids 2–4 come back empty.
        //
        // "This month" is decided on THE SQL SERVER'S CLOCK, from SYSDATETIME() — local server time, not
        // UTC and not the web server's. On one machine that is the same clock; split across an App Service
        // and Azure SQL it is not, and a colonoscopy recorded near midnight on the 1st can fall on either
        // side of the boundary.
        Task<StaffPerformanceResult> GetStaffPerformanceAsync(string staffId);

        // ----- Patient (Admin > Patient) -----
        //
        // dbo.PatientBasic is the registration record every other patient table hangs off: demographics, a
        // residential address, an emergency contact, the iFOBT result, and — when the patient leaves the
        // programme — a discharge reason. A NULL DischargeType_ID IS THE DEFINITION OF AN ACTIVE PATIENT;
        // there is no status column and no IsActive flag (CoreFlow.md §3.8).
        //
        // THREE OF THE SEVEN DECLARE `@User_ID INT = NULL` — spPatientBasic_Insert, spPatientBasic_Update
        // and spPatient_DeleteCascade — which is THE ACTOR for the dbo.AuditTrails row each one writes, so
        // it is absent from all three signatures below and SqlData supplies it from
        // DatabaseHelper.CurrentUserId. The four reads declare no @User_ID and write no audit row.
        //
        // THE ID IS A STRING AND IT IS COMPOSED, NOT AN IDENTITY: 'PAT-' + a six-digit zero-padded
        // sequence, "PAT-000042". See CreatePatientAsync.
        //
        // 🔴 EVERY DERIVATION AND EVERY VALIDATION STAYS IN PatientController AND NONE OF IT IS HERE. The
        // NRIC → birth date and NRIC → gender derivations, the twelve-digit rule, the age arithmetic, the
        // upper-casing of the two name fields, the "clear the iFOBT completion fields unless the status is
        // complete" rule and every user-facing message are the controller's, exactly as they were before
        // this layer existed. These methods send values; they do not decide them. See PatientSaveInput.
        //
        // Lookups are NOT duplicated here. The Patient Edit form's dropdowns — race, source, religion,
        // marital status, occupation, discharge type, and the state/city/postcode tree — all go through the
        // fourteen spLU_* methods at the top of this file, and the appointment half of the same controller
        // reuses GetActiveBranchesAsync and GetAllStaffAsync. One method per procedure, whoever calls it.

        // Every patient still in the programme, for the Admin > Patient list. Calls
        // spPatientBasic_ListActive, whose whole filter is `DischargeType_ID IS NULL`, ordered by
        // Patient_ID DESC — newest first, and the caller must not re-sort.
        //
        // It selects the three iFOBT columns as well as the id and name; /Patient/GetActivePatients
        // projects only the first two. See PatientListItem for why this and the discharged list are two
        // models rather than one shared shape.
        Task<List<PatientListItem>> GetActivePatientsAsync();

        // Every patient who has left the programme. Calls spPatientBasic_ListDischarged — the exact
        // complement of the above (`DischargeType_ID IS NOT NULL`), ordered by
        // Patient_DischargeDate DESC then Patient_ID DESC. The tiebreak is part of the contract: the dates
        // are midnights in a DATETIME column and tie constantly.
        Task<List<PatientDischargedItem>> GetDischargedPatientsAsync();

        // One patient with every column plus the six joined lookup names; null when no patient has that id.
        // Calls spPatientBasic_GetById.
        //
        // All six joins are LEFT JOINs onto lookup tables that nothing constrains the codes to, so any
        // _Name can be null while its _ID is set — a patient holding a retired Race_ID is a state the
        // schema permits. Only DischargeType_Name is projected to the browser today.
        //
        // REUSED BY PROMPT 7: StaffPatientController.GetBasic loads the same row for the clinical pages.
        // Do not add a second method for it.
        Task<PatientBasicDetail?> GetPatientByIdAsync(string patientId);

        // The mandatory document types this patient is MISSING for a given discharge reason. Calls
        // spPatient_Discharge_CheckMissingDocuments.
        //
        // 🔴 AN EMPTY LIST IS THE PASS CONDITION. The procedure returns what is missing, not what is
        // required, so an empty result means "cleared to discharge" and any row at all blocks the save.
        // Reading it the other way round lets every discharge through. See PatientDocumentRequirement.
        //
        // The requirement is data, not code — one dbo.PatientDocumentSettings row per (discharge type,
        // document type) pair, where the row's existence is the rule. A freshly published database has
        // none, so nothing is mandatory until the Settings screen (Prompt 8) is used.
        Task<List<PatientDocumentRequirement>> GetMissingDischargeDocumentsAsync(string patientId,
            string dischargeTypeId);

        // Creates a patient and RETURNS THE NEW Patient_ID, which the caller cannot predict: calls
        // spPatientBasic_Insert, which composes the id itself as
        // 'PAT-' + RIGHT('000000' + sequence, 6) — "PAT-000042" — where the sequence is
        // MAX(CAST(SUBSTRING(Patient_ID, 5, 6) AS INT)) + 1 over the surviving rows. So it REUSES NUMBERS
        // after a delete, exactly like spBranch_Insert and spStaff_Insert, and unlike both of those the
        // prefix is a constant rather than a code — every patient id starts "PAT-" (CoreFlow.md §3.8).
        //
        // 🔴 IT ANSWERS THROUGH AN OUTPUT PARAMETER, NOT A TRAILING SELECT — `@NewPatient_ID VARCHAR(100)
        // OUTPUT` — which makes it the THIRD procedure in nucentra with one, after
        // spUsers_RegisterFailedLogin and spStaffDocument_Delete. SqlData therefore reads it through
        // DynamicParameters; see that method for why the .sql was not changed instead.
        //
        // It VALIDATES NOTHING — no RAISERROR, no uniqueness check, nothing. Two patients may share an
        // NRIC, an email, a phone number and a name; the only rules that exist are PatientController's.
        // The three discharge columns are hard-coded NULL: a new patient is always active.
        // Writes an INSERT row to dbo.AuditTrails.
        Task<string> CreatePatientAsync(PatientSaveInput patient);

        // Updates every column of a patient except the id, INCLUDING the three discharge columns — which is
        // how a patient is discharged, and, because the write is unconditional, how one is un-discharged.
        // Calls spPatientBasic_Update.
        //
        // It is SILENT when the id matches nothing: no error, no audit row (the audit INSERT is guarded by
        // `IF @RowsAffected > 0`), and no row count returned — so this method cannot tell a real write from
        // a missed one, the same asymmetry spBranch_Update and spStaff_Update have (§5.2, §5.4).
        Task UpdatePatientAsync(PatientSaveInput patient);

        // Deletes a patient and everything hanging off them, and hands back the storage keys the caller
        // must clean up. Calls spPatient_DeleteCascade — READ PatientDeleteResult BEFORE CALLING THIS.
        //
        //   • IT CASCADES BY HAND to SEVEN tables, in order: PatientAppointment, PatientJourney,
        //     PatientDocument, PatientFollowUp, PatientColonoscopy, PatientAssessment, then PatientBasic.
        //     Nothing in the schema enforces any of it (there are no foreign keys), and NOTHING BLOCKS THE
        //     DELETE — unlike spStaff_Delete, which refuses when a clinician is still referenced, this
        //     procedure has no guard at all. A patient with a completed journey is removed as readily as
        //     one registered five minutes ago.
        //   • THERE IS NO TRANSACTION AROUND THE SEVEN DELETES. spStaff_Delete wraps its four in
        //     BEGIN TRANSACTION … THROW; this one does not, so a failure partway through leaves the earlier
        //     tables emptied and the patient row present.
        //   • It RETURNS THE BLOB KEYS of the documents it removed rows for, captured before the delete.
        //     Storage is outside the database entirely, so the caller deletes those objects afterwards —
        //     which is not housekeeping: leaving them retains patient data after the patient is gone.
        //   • A delete against an UNKNOWN id is a silent success. Nothing in the result says whether a row
        //     went; only the dbo.AuditTrails row, written solely when one did, records that it happened.
        Task<PatientDeleteResult> DeletePatientCascadeAsync(string patientId);

        // ----- Patient appointments -----
        //
        // A dbo.PatientAppointment row is a patient booked with a staff member, at a branch, on a date, for
        // a strictly on-the-hour block — and it CONSUMES one or more of that staff member's published
        // dbo.StaffSlots hours. The two tables are joined by StaffSlots.PatientAppointment_ID, and 🔴 THAT
        // COLUMN BEING NULL IS THE ENTIRE DEFINITION OF "AVAILABLE" (CoreFlow.md §3.7): there is no
        // IsBooked flag and no released state. Booking stamps the id on; deleting clears it off.
        //
        // TWELVE PROCEDURES ARE WRAPPED HERE, and they split cleanly:
        //
        //   six reads    _ListByPatient, _Search and the four _Lookup* — no @User_ID, no audit row
        //   four writes  _Insert, _Update, _Delete, _UpdateStatus — 🔴 ALL FOUR DECLARE `@User_ID INT =
        //                NULL`, which is THE ACTOR for the dbo.AuditTrails row each one writes. It is
        //                absent from every signature below; SqlData supplies it from
        //                DatabaseHelper.CurrentUserId. Verified against the .sql, not assumed: the six
        //                reads declare no @User_ID at all, and neither does either slot procedure.
        //   two slot     spStaffSlots_AssignAppointment and spStaffSlots_ClearAppointment, which have NO
        //                methods of their own — they exist only inside SaveAppointmentAsync's
        //                transaction. See that method, and the Staff slots banner above.
        //
        // 🔴 THREE OF THE FOUR WRITES ANSWER THROUGH OUTPUT PARAMETERS, which brings nucentra's total from
        // three (§5.6) to six. spPatientAppointment_Insert returns its new identity through
        // `@NewPatientAppointment_ID INT OUTPUT` with no trailing SELECT — the same trap
        // spPatientBasic_Insert sets — and _Update and _UpdateStatus each re-read the saved row into EIGHT
        // `@Out_*` parameters so a caller can audit database state rather than the request payload. All
        // three are read with DynamicParameters in SqlData; none of them can be a QuerySingleAsync.
        //
        // LOOKUPS ARE NOT DUPLICATED HERE. The appointment form's type dropdown is
        // GetJourneyAppointmentTypesAsync, its branch dropdown is GetActiveBranchesAsync, its staff
        // dropdown is GetAllStaffAsync and its slot picker is GetStaffSlotsAsync — all four already exist
        // above, created by Prompts 1, 3 and 4. One method per procedure, whoever calls it.

        // Every appointment belonging to one patient, for the Appointment tab of /Patient/Edit. Calls
        // spPatientAppointment_ListByPatient; ordered by date DESC, then start time DESC, then id DESC —
        // newest first, and the caller must not re-sort.
        //
        // The three joined names are LEFT JOINs onto tables nothing constrains the ids to, so each can be
        // null while its id is set. An unknown patient id is not an error: it returns an empty list.
        Task<List<PatientAppointmentItem>> GetAppointmentsByPatientAsync(string patientId);

        // The appointment search behind BOTH the /Appointment page and /AdminDashboard's "today" panel.
        // Calls spPatientAppointment_Search. EVERY PARAMETER IS OPTIONAL AND NULL MEANS "DO NOT FILTER"
        // (`@X IS NULL OR column = @X`) — so a blank string is NOT the same as no filter and would match
        // nothing. Callers convert blank to null themselves.
        //
        // Two filters are not the plain equality the rest are:
        //   • @ToDate is INCLUSIVE — the procedure compares `< DATEADD(DAY, 1, @ToDate)`, so an
        //     appointment on the end date is returned.
        //   • @PjAppTypeName matches the type's NAME, its ID, or the raw PjAppType_ID stored on the
        //     appointment — three ways, so the dropdown works and legacy rows that stored a name rather
        //     than a code are still findable.
        //
        // The result's PatientAppointment_Date is the DATE column WITH THE START TIME FOLDED IN, under the
        // date column's own name. See AppointmentSearchItem before projecting it.
        Task<List<AppointmentSearchItem>> SearchAppointmentsAsync(string? patientName, string? staffName,
            string? status, DateTime? fromDate, DateTime? toDate, string? pjAppTypeName, string? branchName);

        // The four filter dropdowns on the appointment search page. Each calls one spPatientAppointment_
        // Lookup* procedure, and each returns ONE COLUMN — so all four are List<string> rather than a
        // lookup pair, exactly like GetStaffDocumentStaffNamesAsync.
        //
        // 🔴 ALL FOUR ARE DISTINCT READS OVER dbo.PatientAppointment ITSELF, NOT OVER A LOOKUP TABLE. They
        // answer "what values are actually in use", so a portal with no appointments returns four empty
        // lists, and a branch, patient or clinician with no booking never appears. That is what a filter
        // control wants — offering a value that can only ever return nothing is worse than omitting it —
        // and it is why the branch filter here is NOT GetActiveBranchesAsync.
        //
        // They return NAMES, not ids, and the first three INNER JOIN their parent table: a booking whose
        // Branch_ID, Patient_ID or Staff_ID no longer resolves contributes nothing, and two rows sharing a
        // name collapse into one entry. The search filters on the displayed name, so that is correct.

        // Distinct branch names with at least one appointment, ordered by name. Calls
        // spPatientAppointment_LookupBranches, which additionally requires Branch_Status = 1 — the only
        // one of the four that filters on anything but blankness, so a booking into a branch that has
        // since been deactivated drops out of the filter while the booking itself remains.
        Task<List<string>> GetAppointmentBranchNamesAsync();

        // Distinct patient names with at least one appointment, ordered by name. Calls
        // spPatientAppointment_LookupPatientNames.
        Task<List<string>> GetAppointmentPatientNamesAsync();

        // Distinct staff names with at least one appointment, ordered by name. Calls
        // spPatientAppointment_LookupStaffNames.
        Task<List<string>> GetAppointmentStaffNamesAsync();

        // The distinct statuses actually in use, ordered by value. Calls
        // spPatientAppointment_LookupStatuses.
        //
        // NOTE THAT THIS IS NOT THE LIST OF VALID STATUSES. dbo.PatientAppointment.PatientAppointment_Status
        // is a VARCHAR(100) with no check constraint and no lookup table; the three real values —
        // "Scheduled", "Attended", "Not Attended" — are a convention held in C# HashSets in three
        // controllers and in the .js. This procedure reports what is stored, so a database with no
        // "Not Attended" appointment offers only two entries, and a value written by hand would appear.
        // The appointment FORM's status dropdown is a hard-coded array in the controller, not this.
        Task<List<string>> GetAppointmentStatusesAsync();

        // 🔴 THE SECOND AND LAST OF EXACTLY TWO TRANSACTIONAL UNITS OF WORK IN THIS FILE, and the other of
        // the only two methods in nucentra's data layer that call more than one stored procedure. (The
        // first is SaveStaffWithDocumentsAsync, above.) Everything else here is one method, one procedure.
        //
        // Books or re-books an appointment ATOMICALLY, on one connection inside one SqlTransaction:
        //
        //   1. spStaffSlots_List          — read the staff member's hours for that date  🔴 SEE BELOW
        //   2. (validate the requested slots against what that read returned)
        //   3. spPatientAppointment_Insert  OR  spPatientAppointment_Update
        //   4. spStaffSlots_ClearAppointment — release the hours this appointment held  (update path only)
        //   5. spStaffSlots_AssignAppointment — stamp it onto the hours it now holds
        //   6. commit
        //
        // ── 🔴 WHY STEP 1 IS INSIDE THE TRANSACTION, AND MUST STAY THERE ────────────────────────────────
        //
        // THE READ IS THE CONCURRENCY CHECK. It is not a convenience lookup that happens to be nearby: it
        // is the only thing that decides whether the hours being consumed are still free, and the answer
        // is only worth anything for as long as the transaction that asked holds its locks. Move it out —
        // "read the slots in the controller, validate, then call the save method" — and two administrators
        // booking the same hour both read it as free, both validate, and both write. The second one wins,
        // the first one's appointment silently loses its slot, and nothing anywhere reports it.
        //
        // dbo.StaffSlots has a UNIQUE index on (Staff_ID, SlotDate, SlotStartTime) but NOTHING unique on
        // PatientAppointment_ID, so the database will not catch that double booking either. The
        // read-inside-the-transaction is the whole defence.
        //
        // ── 🔴 WHY THE VALIDATION IS HERE AND THE MESSAGES ARE NOT ──────────────────────────────────────
        //
        // The four slot checks operate on the rows step 1 returned, so they can only live where that read
        // lives. But the sentence a user sees is a presentation decision, so this method returns a TYPED
        // REASON and the controller maps it to the string it has always shown. THE DATA LAYER DECIDES WHAT
        // FAILED; THE CONTROLLER DECIDES WHAT THE USER IS TOLD. Read AppointmentSaveFailure — that
        // convention is the most reusable thing in this area and it is written up there in full.
        //
        // A non-Ok Reason means the transaction was ROLLED BACK and nothing was written. Genuine faults
        // still throw, and a throw also means a rollback.
        //
        // ── The clear-then-assign ordering ──────────────────────────────────────────────────────────────
        //
        // On an UPDATE the appointment's existing hours are cleared BEFORE the new ones are assigned, and
        // the order matters: an hour kept across the edit would otherwise be cleared after being
        // re-assigned and end up free while the appointment believed it held it. Both steps are inside the
        // transaction, so an edit that drops one hour and takes another either does both or neither.
        // spStaffSlots_ClearAppointment is keyed on the APPOINTMENT id, not on a slot list, so it releases
        // every hour the appointment held whether or not the caller knew about it.
        Task<AppointmentSaveResult> SaveAppointmentAsync(AppointmentSaveInput input);

        // Deletes an appointment. Calls spPatientAppointment_Delete.
        //
        // 🔴 IT FREES THE APPOINTMENT'S SLOTS ON THE WAY PAST — `UPDATE dbo.StaffSlots SET
        // PatientAppointment_ID = NULL WHERE PatientAppointment_ID = @id` — before the DELETE, and its
        // comment calls that "FK safety". It is not merely tidiness: FK_StaffSlots_PatientAppointment
        // would refuse the delete outright while any slot still pointed at the row. So deleting an
        // appointment is also what returns those hours to the schedule, and there is no separate step for
        // it. Both statements run bare, with no transaction of their own.
        //
        // It is SILENT when the id matches nothing: no error, no audit row (the audit INSERT is guarded by
        // `IF @RowsAffected > 0`), and no row count returned — so this method cannot tell a real delete
        // from a missed one, the same asymmetry spBranch_Delete and spPatientBasic_Update have.
        Task DeleteAppointmentAsync(int appointmentId);

        // Changes only an appointment's status, and returns the row as it now stands so the caller can
        // audit database state rather than its own request. Calls spPatientAppointment_UpdateStatus.
        //
        // 🔴 UNLIKE EVERY OTHER WRITE IN THIS AREA IT IS NOT SILENT ON A BAD ID: it RAISERRORs
        // 'Appointment not found.' (severity 16) when the UPDATE matches nothing, which arrives here as a
        // SqlException. Both callers answer that with their generic error message plus a correlation id,
        // so the real sentence reaches only Logs/app-*.log.
        //
        // It touches NO slots. An appointment marked "Not Attended" keeps its hours; only a delete or a
        // re-save releases them.
        //
        // CALLED BY TWO CONTROLLERS — /Appointment/UpdateAppointmentStatus, which audits from the result,
        // and /AdminDashboard/UpdateAppointmentStatus, which discards it and writes no AuditLog line at
        // all. See AppointmentStatusResult for why that is one method and not two.
        Task<AppointmentStatusResult> UpdateAppointmentStatusAsync(int appointmentId, string status);

        // ----- Patient journey (Staff > Patient) -----
        //
        // 🔴 THIS IS NUCENTRA'S CORE FEATURE. Read CoreFlow.md §7 before changing anything here; this
        // banner is the short version of it.
        //
        // A dbo.PatientJourney ROW IS AN EVENT, NOT A STATE. One row = "a clinical step of this kind
        // happened to this patient, on this date, under this clinician". Its PjAppType_Name says which of
        // the four LU_PJ_APP_TYPE kinds it was, and three of those four hang a detail table off the row:
        //
        //     01 PATIENT ASSESSMENT  → dbo.PatientAssessment   risks, symptoms, history, investigations
        //     02 COLONOSCOPY         → dbo.PatientColonoscopy  bowel prep and nine per-segment findings
        //     03 FOLLOW UP           → dbo.PatientFollowUp     the HPE result and the discharge plan
        //     04 SURVEILLANCE        → NOTHING. Checked, not assumed: there is no dbo.PatientSurveillance
        //                              table, no procedure and no template. It is a scheduling outcome.
        //
        // 🔴 THERE IS NO STATE MACHINE. No column says which stage a patient is at, nothing stops a
        // follow-up being recorded before an assessment, and there is no transition table — an agent
        // arriving from HEART will look for one and there is none. Ordering is clinical practice, not a
        // constraint. §7 says this at length because it is the single most important thing about this area.
        //
        // ── 🔴 WHY THE SIX WRITES ARE NAMED …WithJourney, AND WHY NONE IS A UNIT OF WORK HERE ───────────
        //
        // Each one writes THREE tables in one call — dbo.PatientJourney, then its detail table, then a
        // dbo.PatientJourneyAudit row — and EACH OWNS ITS OWN TRANSACTION: every one of the six opens with
        // `SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN`, commits at the end, and rolls back and THROWs from its
        // CATCH. Verified by reading all six .sql files, not inferred.
        //
        // So each gets exactly ONE ordinary Dapper method and NO C# transaction. Wrapping one in a
        // SqlTransaction would nest a transaction inside a procedure that already has one and would claim,
        // in the interface, an atomicity guarantee this layer is not the source of. The two transactional
        // units of work in this file remain SaveStaffWithDocumentsAsync and SaveAppointmentAsync, and there
        // is no third.
        //
        // ── 🔴 @User_ID: NOT ONE OF THE TWELVE PROCEDURES IN THIS BANNER DECLARES IT ────────────────────
        //
        // Read every parameter list; do not pattern-match from the other feature areas. None of the three
        // journey reads, none of the three detail reads and none of the six writes declares @User_ID of
        // either kind, and NONE OF THEM WRITES A dbo.AuditTrails ROW. Recording a colonoscopy leaves no
        // trace in nucentra's security trail at all — it writes dbo.PatientJourneyAudit instead
        // (PatientJourneyAuditItem explains the difference between the two trails).
        //
        // What the six writes DO take is @Staff_ID, and IT IS A DIFFERENT IDENTITY. It is the clinician the
        // journey belongs to — an ordinary business argument the controller takes from the caller's
        // "StaffId" claim — and it lands in dbo.PatientJourney.Staff_ID and dbo.PatientJourneyAudit.Staff_ID.
        // IT MUST NEVER BE FILLED FROM DatabaseHelper.CurrentUserId, which is a dbo.Users identity. So
        // Staff_ID appears on the input models and in these signatures, exactly as a business value should.
        //
        // ── The lookup is not duplicated here ───────────────────────────────────────────────────────────
        //
        // The patient-document type dropdown is GetPatientDocumentTypesAsync, created by Prompt 1 for
        // spLU_PatientDocumentType_List. One method per procedure, whoever calls it.

        // One journey row plus its patient's name, for the header of an assessment / colonoscopy /
        // follow-up form. Calls spPatientJourney_GetById; SELECT TOP 1 on the identity, so at most one row
        // and null when the id matches nothing.
        //
        // It INNER JOINs dbo.PatientBasic, so a journey whose patient has been deleted comes back as null
        // rather than as a row with no name. All three GetPatient* endpoints call this FIRST and answer
        // "Journey not found." on null, before they look for a detail row.
        Task<PatientJourneyDetail?> GetJourneyByIdAsync(int patientJourneyId);

        // Every journey a patient has, with who created and who last updated each — the patient's whole
        // clinical timeline in one read. Calls spPatientJourney_TimelineByPatient.
        //
        // ORDERED BY PatientJourney_Date ASC, then PatientJourney_ID ASC, and that ordering is the only
        // sequencing this feature has (there is no stage column to sort on). The caller must not re-sort.
        //
        // The five audit columns come from TWO OUTER APPLYs over dbo.PatientJourneyAudit — the earliest
        // 'CREATED' event and the latest 'UPDATED'/'EDITED' one — which is why every one of them is
        // nullable and why a journey with no audit rows still appears. See PatientJourneyTimelineItem.
        //
        // An unknown or blank patient id is NOT an error: the WHERE matches nothing and the list is empty.
        Task<List<PatientJourneyTimelineItem>> GetJourneyTimelineAsync(string patientId);

        // Every dbo.PatientJourneyAudit event for every journey a patient has, oldest first within each
        // journey. Calls spPatientJourney_AuditsByPatient.
        //
        // This is the FULL history the timeline read above summarises to two rows per journey: one row per
        // …WithJourney call ever made against this patient, 'CREATED' or 'UPDATED', with the clinician and
        // the free-text note they typed. The caller groups it by PatientJourney_ID.
        //
        // 🔴 IT IS NOT dbo.AuditTrails. Two separate database audit trails exist in nucentra and this is
        // the clinical one — keyed on a STAFF member and rendered in the UI, where dbo.AuditTrails is keyed
        // on a USER account and is not. PatientJourneyAuditItem sets the two side by side.
        Task<List<PatientJourneyAuditItem>> GetJourneyAuditsAsync(string patientId);

        // ── The three detail reads ──────────────────────────────────────────────────────────────────────
        //
        // 🔴 THESE THREE RETURN A COLUMN-KEYED DICTIONARY AND NOT A POCO, AND THAT IS DELIBERATE — it is
        // the one place in this file that departs from rule 5, so the reason is written out in full.
        //
        // The endpoint serializes the detail row AS THE PROCEDURE NAMES ITS COLUMNS: the browser receives
        // {"PatientJourney_ID":5,"iFOBTPositive_Date":"…","Risks_Smoking":true,…}, and the three template
        // scripts under wwwroot/js/staffPatient/templates/ read exactly those keys — `d.iFOBTPositive_Date`,
        // `model.HPE_Results`, `Findings_Anus`. ASP.NET Core serializes with JsonSerializerDefaults.Web,
        // which camelCases PROPERTY names but leaves DICTIONARY KEYS alone. So a POCO would ship
        // "patientJourney_ID" and "risks_Smoking" and break all three forms, silently, with a 200 response.
        //
        // A model per detail type would also have to be ~50 properties of pure transcription that nothing
        // in C# ever reads by name — the endpoint passes the whole object straight through.
        //
        // The values are the raw CLR types the reader gives (int, bool, string, DateTime), with DBNull
        // mapped to null, and THE KEY ORDER IS THE PROCEDURE'S SELECT ORDER. Null is returned for "no
        // detail row", which is a real state: all three procedures INNER JOIN their detail table, so asking
        // for a COLONOSCOPY journey's assessment returns nothing while the journey itself still exists.

        // The dbo.PatientAssessment row for one journey, keyed by column name. Calls
        // spPatientAssessment_GetByJourneyId — 51 columns, six from the journey and its patient and 45 from
        // the assessment. Null when this journey has no assessment row.
        Task<IReadOnlyDictionary<string, object?>?> GetAssessmentByJourneyIdAsync(int patientJourneyId);

        // The dbo.PatientColonoscopy row for one journey, keyed by column name. Calls
        // spPatientColonoscopy_GetByJourneyId. Null when this journey has no colonoscopy row.
        Task<IReadOnlyDictionary<string, object?>?> GetColonoscopyByJourneyIdAsync(int patientJourneyId);

        // The dbo.PatientFollowUp row for one journey, keyed by column name. Calls
        // spPatientFollowUp_GetByJourneyId. Null when this journey has no follow-up row.
        Task<IReadOnlyDictionary<string, object?>?> GetFollowUpByJourneyIdAsync(int patientJourneyId);

        // ── The six writes ──────────────────────────────────────────────────────────────────────────────

        // Records a NEW patient assessment: writes dbo.PatientJourney (type 'PATIENT ASSESSMENT'), then
        // dbo.PatientAssessment against the identity that insert produced, then a 'CREATED'
        // dbo.PatientJourneyAudit row — in that order, inside the procedure's own transaction. Calls
        // spPatientAssessment_CreateWithJourney and RETURNS THE NEW PatientJourney_ID.
        //
        // The order is forced: PatientAssessment.PatientJourney_ID is a real foreign key, so the journey
        // row has to exist first, and its id is only known from SCOPE_IDENTITY() afterwards.
        //
        // It answers with a trailing `SELECT @PatientJourney_ID AS PatientJourney_ID` — a genuine result
        // set, NOT the OUTPUT parameter spPatientBasic_Insert and spPatientAppointment_Insert use. Read the
        // .sql rather than the family resemblance; this one really is a QuerySingleAsync.
        //
        // It RAISERRORs (→ SqlException) on an unknown @Patient_ID and on an unknown @Staff_ID, and
        // rolls back. Nothing is written on either path.
        Task<int> CreateAssessmentWithJourneyAsync(PatientAssessmentSaveInput input);

        // Re-saves an existing assessment: UPDATEs dbo.PatientJourney (the date, Updated_At and
        // UpdatedBy_Staff_ID), then UPDATEs dbo.PatientAssessment, then writes an 'UPDATED'
        // dbo.PatientJourneyAudit row. Calls spPatientAssessment_UpdateWithJourney.
        //
        // 🔴 IT CREATES NO SECOND JOURNEY ROW. The whole point of the create/update split is that a
        // re-save must not duplicate the patient's timeline — an update that inserted a journey row would
        // show the same assessment twice, in the right order, with no error anywhere. That is asserted in
        // this prompt's smoke test.
        //
        // RAISERRORs 'Journey not found.' on an unknown journey, 'Staff not found.' on an unknown
        // clinician, and 'Assessment row not found for this journey.' when the journey exists but carries
        // no assessment — each a SqlException here, each rolling back.
        Task UpdateAssessmentWithJourneyAsync(PatientAssessmentSaveInput input);

        // Records a NEW colonoscopy: dbo.PatientJourney (type 'COLONOSCOPY'), then dbo.PatientColonoscopy,
        // then a 'CREATED' audit row, in the procedure's own transaction. Calls
        // spPatientColonoscopy_CreateWithJourney and returns the new PatientJourney_ID from its trailing
        // SELECT.
        //
        // 🔴 IT IS THE ONE CREATE OF THE THREE THAT DOES NOT CHECK THE PATIENT EXISTS. The assessment and
        // follow-up creates both look dbo.PatientBasic up and RAISERROR 'Patient not found.'; this one only
        // refuses a BLANK @Patient_ID. Since Patient_ID is not a foreign key anywhere, a colonoscopy can be
        // recorded against a patient that does not exist. It does still validate @Staff_ID.
        Task<int> CreateColonoscopyWithJourneyAsync(PatientColonoscopySaveInput input);

        // Re-saves an existing colonoscopy — journey row, then detail row, then an 'UPDATED' audit row, and
        // no second journey row. Calls spPatientColonoscopy_UpdateWithJourney.
        //
        // Note the asymmetry with its two siblings: this is the only one of the six writes that ends with
        // NO trailing SELECT at all (the assessment and follow-up updates each emit `SELECT 1 AS Success`,
        // which nothing reads). All three are ExecuteAsync, so it makes no difference here — but it is why
        // none of the three can become a QuerySingleAsync later without reading the .sql first.
        Task UpdateColonoscopyWithJourneyAsync(PatientColonoscopySaveInput input);

        // Records a NEW follow-up: dbo.PatientJourney, then dbo.PatientFollowUp, then a 'CREATED' audit
        // row, in the procedure's own transaction. Calls spPatientFollowUp_CreateWithJourney and returns
        // the new PatientJourney_ID.
        //
        // 🔴 IT WRITES PjAppType_Name = 'PATIENT FOLLOW UP', WHICH IS NOT THE LU_PJ_APP_TYPE VALUE
        // ("FOLLOW UP"). Nothing joins the column to the lookup, so nothing reports the mismatch, and the
        // string the procedure writes is the one the portal switches on. See PatientFollowUpSaveInput.
        Task<int> CreateFollowUpWithJourneyAsync(PatientFollowUpSaveInput input);

        // Re-saves an existing follow-up — journey row, then detail row, then an 'UPDATED' audit row, and
        // no second journey row. Calls spPatientFollowUp_UpdateWithJourney.
        Task UpdateFollowUpWithJourneyAsync(PatientFollowUpSaveInput input);

        // ----- Patient documents -----
        //
        // The patient half of nucentra's document storage: a dbo.PatientDocument row is the CATALOGUE
        // ENTRY, and the bytes live in a private Azure Blob container reached only through a short-lived
        // read SAS minted per click. DOCUMENTSTORAGE.md is authoritative on the mechanism and this
        // migration changes none of it.
        //
        // FIVE PROCEDURES ARE WRAPPED HERE. 🔴 THE @User_ID PICTURE IS NOT UNIFORM AND MUST BE READ FILE BY
        // FILE — this is the one feature area in the plan where grepping actively misleads:
        //
        //   spPatientDocument_Insert   `@User_ID INT = NULL` — ACTOR. Supplied by SqlData from the claim.
        //   spPatientDocument_Delete   `@User_ID INT = NULL` — ACTOR. Same. (It ALSO has an OUTPUT
        //                              parameter — see below.)
        //   spPatientDocument_GetById  🔴 DOES NOT DECLARE @User_ID AT ALL, despite its header comment
        //                              SAYING the words "no @User_ID and no audit row". A grep for
        //                              "@User_ID" matches that comment and would put you one step from
        //                              sending a parameter the procedure has no place for. Read the
        //                              parameter list: it is `@PatientDocument_ID INT` and nothing else.
        //   spPatientDocument_List     no @User_ID, no audit row.
        //   spPatientDocument_LookupDocuments  no @User_ID, no audit row.
        //
        // spPatientDocument_PatientNames and spDocuments_Search live in the same folder and belong to
        // Prompt 8. They are not wrapped here.

        // Every document belonging to one patient, newest first. Calls spPatientDocument_List, ordered
        // `UploadedOn DESC, PatientDocument_ID DESC` — and 🔴 THAT FIRST KEY IS A STRING SORT, because
        // UploadedOn is VARCHAR(100) rather than a date (see PatientDocumentItem). The identity tiebreak is
        // part of the contract, not decoration: two documents uploaded in one request share a timestamp.
        //
        // Unlike its staff twin @Patient_ID is REQUIRED here — spStaffDocument_List's is optional and
        // returns every document in the system when omitted; this one has no such mode. An unknown id
        // returns an empty list.
        //
        // The result carries BlobName. The endpoint deliberately does not project it.
        Task<List<PatientDocumentItem>> GetPatientDocumentsAsync(string patientId);

        // One document by id, or null. Calls spPatientDocument_GetById — the same nine columns
        // GetPatientDocumentsAsync returns, hence the same model.
        //
        // It exists to resolve a blob key and a file name so a five-minute read SAS can be minted, and it
        // is called TWICE per delete-and-download cycle: once by GetPatientDocumentUrl, and once by
        // DeletePatientDocument purely so the audit line can name the patient the document belonged to
        // (spPatientDocument_Delete hands back only the blob key).
        //
        // 🔴 REUSED BY PROMPT 8 — DocumentsController's own download endpoint calls this same method. It is
        // created here and must not be duplicated there; see DapperLayerPlan.md's shared-procedure table.
        //
        // No @User_ID: it is read-only and writes no audit row. Its header comment says so in words that a
        // grep mistakes for a declaration.
        Task<PatientDocumentItem?> GetPatientDocumentByIdAsync(int patientDocumentId);

        // Records one uploaded document. Calls spPatientDocument_Insert.
        //
        // 🔴 THE BYTES ARE ALREADY IN THE CONTAINER when this runs — the caller uploads and hands over the
        // resulting key. CRC.Data has no reference to CRC.Web and cannot see IDocumentStorage. See
        // PatientDocumentInput.
        //
        // spPatientDocument_Insert declares @User_ID INT = NULL — the ACTOR for the dbo.AuditTrails row it
        // writes — and SqlData supplies it from DatabaseHelper.CurrentUserId, which is why it is absent
        // from this signature. Before the Dapper layer, DatabaseHelper appended it automatically off
        // sys.parameters and the controller never mentioned it; drop it now and every patient-document
        // upload audits as user 0, with nothing failing.
        //
        // It does NOT return the new PatientDocument_ID. The procedure computes it from SCOPE_IDENTITY()
        // only to name it in the audit summary and then discards it — which is why every
        // AuditLog.PatientDocumentUploaded line in the portal records DocumentId=0 and identifies the row
        // by its blob key instead. Exactly the same shape spStaffDocument_Insert has.
        Task AddPatientDocumentAsync(PatientDocumentInput document);

        // Deletes one document row and RETURNS ITS BLOB KEY so the caller can remove the object from
        // storage. Calls spPatientDocument_Delete. Null means no row matched — and therefore nothing to
        // remove — which is also what the caller gets for an id that never existed.
        //
        // The key comes back through `@DeletedBlobName VARCHAR(500) = NULL OUTPUT`, so this is one of the
        // handful of calls in SqlData that needs DynamicParameters; the staff twin
        // DeleteStaffDocumentAsync is written the same way for the same reason.
        //
        // Storage cannot join a database transaction, so the row goes first and the object second, on a
        // best-effort basis — the caller logs a failed removal as a warning rather than failing the
        // request, because from the user's side the document HAS been deleted and what is left is an
        // orphaned blob for an operator to clean up.
        //
        // spPatientDocument_Delete declares @User_ID INT = NULL — the ACTOR — supplied by SqlData. The
        // audit row is written only when a row was actually deleted.
        Task<string?> DeletePatientDocumentAsync(int patientDocumentId);

        // The patient document-type filter for Prompt 8's Documents search page. Calls
        // spPatientDocument_LookupDocuments, ordered by the resolved name.
        //
        // 🔴 IT IS NOT THE SAME LIST AS GetPatientDocumentTypesAsync, AND BOTH ARE CORRECT. This one UNIONS
        // the types actually in use on dbo.PatientDocument with the types in LU_PATDOCUMENTTYPE, and
        // COALESCEs a missing name back to the raw id. It exists because PatientDocument.
        // PatientDocumentType_ID has no foreign key: a document uploaded under a type later removed from
        // the lookup must still be FINDABLE, so the filter offers the orphaned id with the id as its own
        // label. An UPLOAD form must not offer that type, which is why it uses spLU_PatientDocumentType_List
        // instead. Two procedures, two right answers, one lookup table — exactly the split
        // spStaffDocument_LookupDocuments makes on the staff side.
        //
        // NO CALLER YET. It is wrapped here because Prompt 7 owns the spPatientDocument_* family; Prompt 8
        // wires it to the Documents page. Do not create a second method for it there.
        Task<List<LookupItem>> GetPatientDocumentTypeFiltersAsync();

        // ----- Document settings (Admin > Settings) -----
        //
        // The document RULES, not the documents: which document types a discharge reason or a staff type
        // REQUIRES. Two tiny tables, dbo.PatientDocumentSettings and dbo.StaffDocumentSettings, each a
        // composite-key row whose EXISTENCE is the rule — there is no "mandatory" flag stored anywhere.
        // Both are edited by the one SUPERUSER Settings screen. See CoreFlow.md §3.16, §3.17 and §8.
        //
        // NONE OF THE FIVE PROCEDURES IN THIS AREA DECLARES @User_ID. Every read and every write here is
        // outside the dbo.AuditTrails mechanism entirely: changing what the whole programme treats as a
        // mandatory document writes NO audit row, in either channel. That is the state of the code, not a
        // recommendation — see §8.
        //
        // 🔴 THE TWO FAMILIES SAVE DIFFERENTLY, AND THE ASYMMETRY IS THE THING TO KNOW ABOUT THIS AREA.
        // The patient side replaces a discharge type's whole set in ONE procedure —
        // spPatientDocumentSettings_SaveForDischargeType deletes and re-inserts inside a single statement
        // batch, so it is implicitly atomic. The staff side has NO such procedure: the caller deletes the
        // staff type's rows and then inserts the new ones ONE AT A TIME, N+1 round trips with no
        // transaction around them, exactly as SettingsController has always done it. A failure between the
        // delete and the last insert leaves that staff type with a PARTIAL set of mandatory documents and
        // reports an error, and nothing rolls it back.
        //
        // That is a real difference in safety and it is left ALONE on purpose: adding a transaction would
        // be a behaviour change, and the Dapper migration does not make behaviour changes. It is written
        // down here, and in CoreFlow.md §5.9, so that whoever decides to fix it does so deliberately. The
        // sequencing stays in the controller — these two methods are one procedure each, per the rule at
        // the top of this file, and neither is a unit of work.
        //
        // (spStaffDocumentSettings_GetByStaffType, the staff read, is NOT here: it was wrapped in Prompt 3
        // as GetStaffDocumentSettingsAsync under `----- Staff documents -----`, because StaffController's
        // mandatory-document check needs it too. Reuse it; do not add a second method.)

        // The document types a patient must have on file before being discharged under one reason. Calls
        // spPatientDocumentSettings_GetByDischargeType, ordered by the document type's name.
        //
        // 🔴 IT RETURNS ONLY THE MANDATORY TYPES — the opposite of GetStaffDocumentSettingsAsync, which
        // returns every type with an IsMandatory flag. An empty list means "nothing is required for this
        // discharge reason", which is also what an UNKNOWN dischargeTypeId returns: the procedure filters
        // and does not validate. See PatientDocumentSetting.
        Task<List<PatientDocumentSetting>> GetDischargeDocumentSettingsAsync(string dischargeTypeId);

        // Replaces the WHOLE set of mandatory document types for one discharge reason. Calls
        // spPatientDocumentSettings_SaveForDischargeType.
        //
        // 🔴 THE IDS ARRIVE AS ONE COMMA-SEPARATED STRING, not as a list, because the procedure takes
        // `@PatientDocumentType_IDs NVARCHAR(MAX)` and splits it with STRING_SPLIT. NULL or blank is not an
        // error and not a no-op: it CLEARS the discharge reason's settings, which is how the screen saves
        // an empty checklist. The caller builds the CSV.
        //
        // The procedure does the delete and the re-insert itself, in one batch, so the replace cannot be
        // observed half-done — unlike the staff side above. It RAISERRORs severity 11 on an unrecognised
        // @DischargeType_ID (the only validation in this area), which surfaces as a SqlException; ids that
        // do not match LU_PATDOCUMENTTYPE are silently dropped by an INNER JOIN instead.
        Task SaveDischargeDocumentSettingsAsync(string dischargeTypeId, string? patientDocumentTypeIdsCsv);

        // Clears every mandatory-document row for one staff type. Calls
        // spStaffDocumentSettings_DeleteByStaffType — a bare DELETE … WHERE StaffType_ID = @StaffType_ID.
        //
        // 🔴 STEP ONE OF TWO. This is not a save on its own: the caller follows it with one
        // AddStaffDocumentSettingAsync per selected type, outside any transaction. See the banner above.
        // An unknown staff type deletes nothing and does not complain.
        Task DeleteStaffDocumentSettingsAsync(string staffTypeId);

        // Records ONE document type as mandatory for a staff type. Calls spStaffDocumentSettings_Insert —
        // a bare INSERT with no upsert and no existence check, so calling it twice for the same
        // (StaffType_ID, StaffDocumentType_ID) violates the composite primary key and throws.
        //
        // 🔴 STEP TWO OF TWO, CALLED N TIMES. It is safe only after DeleteStaffDocumentSettingsAsync has
        // cleared the staff type, which is why the two are never reordered and why the caller de-duplicates
        // its list first.
        //
        // Both NAMES are denormalized onto the row and neither is validated against its lookup: the staff
        // type's name is whatever the client posted, and the document type's name is looked up by the
        // caller from spLU_STAFFDOCUMENTTYPE_List. The patient-side procedure resolves its names in SQL
        // instead — another face of the same asymmetry.
        Task AddStaffDocumentSettingAsync(string staffTypeId, string? staffTypeName,
            string staffDocumentTypeId, string staffDocumentTypeName);

        // ----- Documents (the SUPERUSER search page) -----
        //
        // Two procedures behind /Documents, the one page in nucentra that looks across BOTH document
        // families at once. Neither declares @User_ID; the search is audited by the application instead
        // (AuditLog.DocumentSearched), because a search over patient records is a read worth recording and
        // no procedure writes a dbo.AuditTrails row for a read.

        // The document search itself. Calls spDocuments_Search.
        //
        // 🔴 ONE PROCEDURE, THREE BRANCHES, AND @Mode CHOOSES THE TABLE. 'PATIENT' reads
        // dbo.PatientDocument, 'STAFF' reads dbo.StaffDocument, and ANYTHING ELSE — including NULL and a
        // misspelling — falls through to a `WHERE 1 = 0` branch that returns the same seven columns and no
        // rows. So an unrecognised mode is EMPTY, NOT AN ERROR, and the mode is matched after
        // UPPER(LTRIM(RTRIM(…))). The caller normalises to "Patient"/"Staff" before it gets here anyway.
        //
        // Both filters are EXACT (equality after UPPER/TRIM), not LIKE: the page's controls are dropdowns
        // of values the database already holds, so there is nothing to prefix-match. Passing null means
        // "no filter" — and so does an empty or whitespace string, which the procedure NULLIFs itself.
        //
        // documentType matches EITHER the type's NAME or its ID, in the same OR: the page sends whichever
        // its dropdown had, and the two document families disagree about which one that is. Every
        // comparison wraps both sides in UPPER(LTRIM(RTRIM(ISNULL(…)))), so no index is usable — fine at
        // this table size, and the reason to notice before the tables grow.
        //
        // The two branches ORDER differently and both orderings are the page's contract: the patient branch
        // sorts by TRY_CONVERT(DATETIME, UploadedOn, 120) DESC because that column is a VARCHAR (§3.15),
        // the staff branch by the real DATETIME column DESC.
        Task<List<DocumentSearchItem>> SearchDocumentsAsync(string mode, string? individualName,
            string? documentType);

        // The patient filter on the same page: the distinct names of every patient who owns at least one
        // document, ordered by name. Calls spPatientDocument_PatientNames.
        //
        // The exact twin of GetStaffDocumentStaffNamesAsync, down to the reasoning: it returns NAMES, NOT
        // IDS, because the filter control filters on the displayed name — so two patients sharing a name
        // collapse into one entry and select the documents of both. Its INNER JOIN onto dbo.PatientBasic
        // means a document whose Patient_ID no longer matches a patient contributes nothing and its owner
        // cannot be picked from the filter, though the document still appears in an unfiltered search.
        Task<List<string>> GetPatientDocumentPatientNamesAsync();

        // ----- Dashboard (SUPERUSER) -----
        //
        // Four aggregates behind /Dashboard, one card and three charts. None declares @User_ID, none takes
        // a parameter, and none has a filter the caller can influence: the SUPERUSER dashboard is the whole
        // programme, not a slice of it. Each is a single GROUP BY over dbo.PatientBasic or a COUNT over
        // dbo.Branch, so the database does the arithmetic and the endpoint only renames the columns.
        //
        // 🔴 THE THREE CHARTS DO NOT SHARE A DENOMINATOR, and that is the thing to know before reading one
        // against another. Race and age count EVERY patient; discharge counts only patients with a
        // DischargeType_ID, which is the definition of a discharged one (§3.8). Race and discharge are
        // ordered by count descending — with no tie-breaker, so equal counts arrive in an arbitrary order —
        // while age is ordered by a CASE that puts its buckets in age order. Each procedure's ORDER BY is
        // its chart's axis; nothing re-sorts them afterwards.

        // How many branches are switched on. Calls spDashboard_Branch_CountActive — `COUNT(*) … WHERE
        // ISNULL(Branch_Status, 0) = 1` over dbo.Branch, which is one row and one column, always.
        //
        // It is the only number on the dashboard that is not about patients, and the ISNULL is defensive
        // rather than load-bearing: Branch_Status is BIT NOT NULL (§3.2).
        Task<int> GetActiveBranchCountAsync();

        // The race pie: how many patients of each race. Calls spDashboard_Patient_ByRace.
        //
        // A LEFT JOIN onto LU_RACE with the name COALESCEd, so a Race_ID matching no lookup row and a race
        // with a blank name both group together under the literal "Unknown". Ordered by count descending.
        // See PatientsByRaceItem.
        Task<List<PatientsByRaceItem>> GetPatientsByRaceAsync();

        // The age pie: how many patients in each of five age bands. Calls spDashboard_Patient_ByAgeGroup.
        //
        // The bands — 20 and below / 21-40 / 41-60 / 61-80 / 81 and above — exist ONLY in that procedure's
        // CASE expression, with no gap and no overlap. A sixth branch, "Unknown", is unreachable while
        // Patient_Age is INT NOT NULL. Ordered by age, not by count. See PatientsByAgeGroupItem.
        Task<List<PatientsByAgeGroupItem>> GetPatientsByAgeGroupAsync();

        // The discharge bar chart: how many patients left the programme under each reason. Calls
        // spDashboard_Patient_ByDischargeType.
        //
        // 🔴 THE ONLY ONE OF THE THREE WITH A `WHERE` — `DischargeType_ID IS NOT NULL` — so its total is
        // the discharged population and not the patient population. Ordered by count descending. See
        // PatientsByDischargeTypeItem.
        Task<List<PatientsByDischargeTypeItem>> GetPatientsByDischargeTypeAsync();

        // ----- Staff dashboard (STAFF) -----
        //
        // Three reads behind /StaffDashboard, the landing page of a UserType = 3 account. They are the same
        // nine columns over the same joins three times, differing only in the date window: one day, the
        // rolling next seven days, and one calendar month. None declares @User_ID; none writes anything.
        //
        // 🔴 `staffId` IS THE ACCESS CONTROL, AND IT IS AN ORDINARY ARGUMENT HERE ON PURPOSE.
        // Every one of the three filters on `pa.Staff_ID = @Staff_ID`, and that predicate is the only thing
        // standing between a clinician and every other clinician's diary. The value comes from the caller's
        // own `StaffId` claim, and StaffDashboardController resolves it — refusing the request with a
        // message when the claim is absent — BEFORE calling any of these. That resolution deliberately does
        // not live in this layer: `@User_ID`'s actor value is injected here because it is bookkeeping
        // nobody should have to remember, but a scoping predicate is a business argument, and a data-access
        // method that reached for the current user's claim to fill one would be doing authorization out of
        // sight of the endpoint that is accountable for it. See CoreFlow.md §4.13.
        //
        // A staff id that matches nothing returns an EMPTY LIST, not an error — including a well-formed id
        // belonging to somebody else, which is what makes the predicate a filter rather than a check. The
        // three procedures order by date, then start time, then id; the endpoint then re-sorts by the same
        // keys in C# (§4.13) — redundant, preserved, and harmless.

        // One staff member's appointments on one date. Calls spStaffDashboard_TodayAppointments; the
        // endpoint always passes DateTime.Today, but the procedure takes any date.
        Task<List<StaffDashboardAppointmentItem>> GetStaffTodayAppointmentsAsync(string staffId, DateTime forDate);

        // One staff member's next seven days. Calls spStaffDashboard_ThisWeekAppointments.
        //
        // 🔴 A ROLLING WINDOW, NOT A CALENDAR WEEK: `>= @FromDate AND < DATEADD(DAY, 7, @FromDate)`, start
        // inclusive and end exclusive. It has nothing to do with Monday, and the card's "this week" label
        // means "the next seven days from today" — today included, the same day next week excluded.
        Task<List<StaffDashboardAppointmentItem>> GetStaffWeekAppointmentsAsync(string staffId, DateTime fromDate);

        // One staff member's calendar month. Calls spStaffDashboard_ThisMonthAppointments, which builds its
        // own window with DATEFROMPARTS(@Year, @Month, 1) and DATEADD(MONTH, 1, …) — so month length and
        // leap years are the database's problem, not the caller's.
        //
        // 🔴 DATEFROMPARTS THROWS ON AN OUT-OF-RANGE MONTH rather than returning nothing, which is why the
        // endpoint validates 1-12 before calling and answers "Invalid month value." itself. The year is not
        // validated anywhere: DATEFROMPARTS accepts 1 through 9999 and a year outside that raises the same
        // SQL error, caught as a generic failure message.
        Task<List<StaffDashboardAppointmentItem>> GetStaffMonthAppointmentsAsync(string staffId, int year, int month);

        // ----- Patient tracker -----
        //
        // Five reads behind /PatientTracker, the ADMIN-or-SUPERUSER grid of every patient against the four
        // journey types. None takes a parameter, none declares @User_ID, and none writes: the page loads the
        // whole programme in one round of five queries and does all of its filtering in the browser.
        //
        // 🔴 "STALLED" IS DEFINED TWICE, IN SQL, AND NOWHERE ELSE. A patient is stalled when they have at
        // least one appointment AND the status of their most recent one — by date, then start time, then id,
        // descending — is not 'Scheduled'. A patient with no appointment at all is not stalled.
        // spPatientTracker_Patients_List computes it per patient as an IsStalled bit over a LEFT JOIN;
        // spPatientTracker_StalledCount_Get computes the same thing as a COUNT over an INNER JOIN. The two
        // agree today because the two CTEs are character-for-character the same. Nothing enforces that, no
        // test covers it, and the failure mode is a badge that disagrees with the rows beneath it. If you
        // change one, change both — and see CoreFlow.md §5.10, which is where this rule is written down.

        // The four journey types, for the tracker's column headers. Calls
        // spPatientTracker_AppointmentTypes_List, ordered by id.
        //
        // IT IS A SECOND PROCEDURE OVER LU_PJ_APP_TYPE, returning what spLU_PJ_AppType_List already
        // returns, in the same order. Two procedures, therefore two methods, per the one-method-per-
        // procedure rule at the top of this file — GetJourneyAppointmentTypesAsync is the lookup one. This
        // is not a duplicate to collapse; collapsing it would put a call site in one feature's data path on
        // another feature's procedure, and the .sql files are what would then drift apart unnoticed.
        Task<List<LookupItem>> GetTrackerAppointmentTypesAsync();

        // Every patient, with the stalled flag. Calls spPatientTracker_Patients_List, ordered by name then
        // id. Active and discharged alike — the tracker is a whole-programme view. See
        // PatientTrackerPatientItem for what IsStalled means.
        Task<List<PatientTrackerPatientItem>> GetTrackerPatientsAsync();

        // The latest booking per patient per journey type. Calls spPatientTracker_Appointments_List.
        //
        // ONE ROW PER (Patient_ID, PjAppType_ID) — the newest, by date then start time then id — not one
        // row per appointment. The result set has no outer ORDER BY because the caller indexes it by the
        // two ids. See PatientTrackerAppointmentItem.
        Task<List<PatientTrackerAppointmentItem>> GetTrackerAppointmentsAsync();

        // Every completed journey step. Calls spPatientTracker_Procedures_List — a bare read of
        // dbo.PatientJourney, ALL rows, ordered by patient then date then id.
        //
        // The counterpart of the list above: that one is what was booked, this one is what was done, and
        // the page shows both because nothing in the schema ties a journey row to an appointment. Note that
        // this one carries the type's NAME (dbo.PatientJourney stores no PjAppType_ID) where that one
        // carries its ID.
        Task<List<PatientTrackerProcedureItem>> GetTrackerProceduresAsync();

        // The stalled badge. Calls spPatientTracker_StalledCount_Get — one row, one column, always.
        //
        // The same definition as IsStalled above, computed independently over an INNER JOIN onto
        // dbo.PatientBasic, so an appointment whose Patient_ID no longer matches a patient is excluded
        // here. The per-patient list LEFT JOINs from the patient side and cannot see such a row at all, so
        // the two still agree — for a reason neither procedure states.
        Task<int> GetTrackerStalledCountAsync();

        // ----- Audit trails (SUPERUSER) -----
        //
        // Four reads behind /AuditTrails, the page where the whole plan's `@User_ID` work becomes visible to
        // a human: dbo.AuditTrails holds the rows nineteen stored procedures write from inside themselves,
        // and these four are the only things in the portal that read them. Nothing here writes an audit row
        // — auditing the reading of the audit trail is not a thing this codebase does.
        //
        // 🔴 NONE OF THE FOUR DECLARES `@User_ID`, and spAuditTrails_Search's `@UserId INT = NULL` is NOT an
        // exception to that — different name, different meaning, and the difference is the whole point.
        // `@User_ID` (with the underscore) is the actor a write procedure records; `@UserId` here is a
        // FILTER over rows already written, an ordinary argument the SUPERUSER chooses from a dropdown.
        // Filling it from DatabaseHelper.CurrentUserId would silently narrow every search to the searcher's
        // own actions. It is the one place in the codebase where the two spellings sit close enough to be
        // confused, which is why they are named apart here. See CoreFlow.md §0.1 and §9.
        //
        // The three lookup procedures read their values back out of dbo.AuditTrails itself with SELECT
        // DISTINCT rather than from any catalogue, so the filter dropdowns can only ever offer combinations
        // that have actually happened — and an action or category that has never been recorded is not
        // offerable, not even to prove it returns nothing.

        // The audit search. Calls spAuditTrails_Search; every one of the five filters is optional and NULL
        // means "no filter". Ordered by AuditTrail_EventUTC descending — newest first.
        //
        // 🔴 THE DATES ARE MALAYSIAN, THE STORAGE IS UTC. The procedure compares
        // `DATEADD(HOUR, 8, AuditTrail_EventUTC)` against the bounds and returns that same shifted value as
        // AuditTrail_EventMYT, so what is filtered is what is displayed. `@ToDate` is INCLUSIVE of its whole
        // day — the predicate is `< DATEADD(DAY, 1, @ToDate)` — so a from/to pair naming one date returns
        // that entire Malaysian day. Both are DATE parameters: a time of day on either is discarded.
        //
        // @Action and @Category are EXACT matches, not LIKE, because both come from the dropdowns below.
        Task<List<AuditTrailSearchItem>> SearchAuditTrailsAsync(int? userId, DateTime? fromDate,
            DateTime? toDate, string? action, string? category);

        // The actor filter: every user who appears in dbo.AuditTrails, as (id, name). Calls
        // spAuditTrails_LookupUsers.
        //
        // 🔴 ITS `INNER JOIN dbo.Users` HIDES EXACTLY THE ROWS SOMEBODY LOOKING AT AN AUDIT TRAIL MOST WANTS
        // TO FIND. An audit row whose actor was never recorded (User_Id = 0 — the silent failure of §0.1) or
        // whose user has since been deleted joins to nothing and its actor is not offered in the dropdown,
        // even though the rows themselves are returned by the search above with a blank name. The dropdown
        // is therefore not a complete list of who is in the table. Query dbo.AuditTrails directly to be
        // sure — `SELECT DISTINCT User_Id FROM dbo.AuditTrails` is the honest version.
        //
        // Comes back as LookupItem: the procedure's User_ID is an INT, and the ordinal-mapping helper
        // stringifies it, which is exactly what the endpoint's JSON has always carried ("id": "4").
        Task<List<LookupItem>> GetAuditTrailUsersAsync();

        // The action filter — the distinct AuditTrail_Action values actually present, ordered. Calls
        // spAuditTrails_LookupActions. In practice INSERT, UPDATE and DELETE, because that is all the
        // nineteen writing procedures ever record; the column is a VARCHAR(20) with no constraint, so it is
        // a fact about the data rather than a promise.
        Task<List<string>> GetAuditTrailActionsAsync();

        // The category filter — the distinct AuditTrail_Category values actually present, ordered. Calls
        // spAuditTrails_LookupCategories. The categories name the TABLE a write touched (Branch, Staff,
        // StaffSlots, PatientBasic, PatientAppointment, PatientDocument, StaffDocument), not the screen it
        // came from, and both lookups drop NULLs and blanks before the DISTINCT.
        Task<List<string>> GetAuditTrailCategoriesAsync();

        // ----- Agent API (machine-callable surface — CoreFlow.md §13) -----
        //
        // The endpoints under /api/agent are called by an external n8n workflow over HTTP with a shared
        // key, not by a browser with a cookie. Everything they read they read through this interface like
        // every other caller; the only thing that is different about them is who is asking, and the one
        // method below is how the portal answers that question.

        // The dbo.Users row the Agent API performs its work as — Username = 'AGENT_SERVICE', seeded by
        // CRC.Database/Scripts/Seed_Users.sql and resolved once per agent request by AgentApiKeyFilter,
        // before any action runs. Calls spAgentUsers_GetServiceAccount, which returns four columns and
        // deliberately not Password_Hash or User_Email: the caller wants an identity to build a principal
        // from, not a credential to check.
        //
        // No @User_ID, of either kind, and the procedure declares none. It writes nothing, so there is no
        // dbo.AuditTrails row and no actor to record; and it runs BEFORE ANY PRINCIPAL EXISTS — an
        // API-key request arrives with no cookie, DatabaseHelper.CurrentUserId is null, and this call is
        // what creates the identity the rest of the request runs as. Asking it for an actor would be
        // asking it for the answer it is being called to produce.
        //
        // The lookup is by USERNAME, not by a configured id, and that is not a style choice:
        // dbo.Users.User_ID is INT IDENTITY, so this account's id on a local CRC_DB and its id on Azure
        // SQL are different numbers, and a stale setting would not throw — it would write a different,
        // real user's id into dbo.AuditTrails and report success. IX_Users_Username is UNIQUE, so the
        // lookup is an index seek and the answer cannot be ambiguous.
        //
        // 🔴 NULL IS NOT A NORMAL OUTCOME AND THE CALLER IS REQUIRED TO FAIL ON IT. It means the database
        // was published without the seed. The caller answers 503, logs an error and executes no action —
        // it must NOT fall through to a null actor, a zero, or any other default. Name the failure that
        // rule prevents, because it is silent and permanent: spPatientAppointment_Insert writes
        // ISNULL(@User_ID, 0), so every appointment the agent booked would be audited as
        // AuditTrails.User_Id = 0 — nobody — with no error, no failed request and no way to reconstruct
        // afterwards who did what (§0.1). This repository has already shipped that bug once.
        Task<AgentServiceAccount?> GetAgentServiceAccountAsync();
    }
}
