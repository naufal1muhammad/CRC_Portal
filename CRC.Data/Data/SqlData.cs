using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using CRC.Data.Models;

namespace CRC.Data.Data
{
    // =================================================================================================
    // THE ONLY PLACE IN THE SOLUTION THAT NAMES A STORED PROCEDURE.
    //
    // If you are grepping for "sp" to find out who calls something, this file is the whole answer. That
    // is the property worth protecting: a procedure's signature changes here, once, and the compiler
    // finds every caller — rather than a string literal in a controller finding a user.
    //
    // Every call is `commandType: CommandType.StoredProcedure`, with an anonymous parameter object whose
    // property names match the procedure's parameters without the `@`. THERE IS NO INLINE SQL ANYWHERE IN
    // NUCENTRA AND NONE IS TO BE ADDED — not a SELECT, not a one-line UPDATE, not "just for this". A new
    // query means a new .sql file under CRC.Database/Stored Procedures/{Feature}/, registered in
    // CRC.Database.sqlproj, and a new method here.
    //
    // Pick the Dapper verb by what the procedure guarantees, not by what today's caller happens to want:
    // QuerySingleOrDefaultAsync for a row that may not exist, QuerySingleAsync when exactly one is
    // guaranteed, QueryAsync for a set, ExecuteAsync for a write with no result set, QueryMultipleAsync
    // for several result sets (read the grids in the order the procedure emits them).
    //
    // 🔴 @User_ID. 19 procedures declare `@User_ID INT = NULL` — the ACTOR for their dbo.AuditTrails row.
    // DatabaseHelper used to append that parameter automatically off sys.parameters; Dapper cannot, so it
    // is passed here explicitly as `User_ID = _databaseHelper.CurrentUserId`, with a comment on every one
    // of the 19 saying it is the actor. Five spUsers_* procedures declare `@User_ID INT` with no default,
    // meaning a TARGET user row, which arrives as a method argument instead. The full lists, and why the
    // difference matters, are in IDatabaseData.cs and CoreFlow.md §0. Omitting an actor parameter throws
    // nothing and writes AuditTrails.User_Id = 0.
    //
    // Ordering: the banners here, and the methods inside them, MATCH IDatabaseData.cs exactly, so the two
    // files can be read side by side.
    // =================================================================================================
    public class SqlData : IDatabaseData
    {
        private readonly DatabaseHelper _databaseHelper;

        public SqlData(DatabaseHelper databaseHelper)
        {
            _databaseHelper = databaseHelper;
        }

        // Methods are added here by Prompts 1-9. Each opens its own connection with
        // `using var connection = _databaseHelper.CreateConnection();` — Dapper opens and closes it
        // around the call, so nothing here manages connection lifetime by hand except the two
        // transactional units of work, which own a connection and a SqlTransaction deliberately.

        // ----- Lookups (LU_* reference data) -----

        // 🔴 THE ONE PLACE IN THIS FILE THAT READS A COLUMN BY ORDINAL, and it is deliberate.
        //
        // Eleven of the fourteen lookup procedures return exactly two columns — a VARCHAR(100) code and
        // its VARCHAR(100) display name — and every one of them names those columns after its own table:
        //
        //     spLU_DischargeType_List        DischargeType_ID       DischargeType_Name
        //     spLU_MaritalStatus_List        MaritalStatus_ID       MaritalStatus_Name
        //     spLU_Occupation_List           Occupation_ID          Occupation_Name
        //     spLU_ORGANIZATION_List         Organization_ID        Organization_Name
        //     spLU_PatientDocumentType_List  PatientDocumentType_ID PatientDocumentType_Name
        //     spLU_PJ_AppType_List           PjAppType_ID           PjAppType_Name
        //     spLU_Race_List                 Race_ID                Race_Name
        //     spLU_Religion_List             Religion_ID            Religion_Name
        //     spLU_Source_List               Source_ID              Source_Name
        //     spLU_STAFFDOCUMENTTYPE_List    StaffDocumentType_ID   StaffDocumentType_Name
        //     spLU_STAFFTYPE_List            StaffType_ID           StaffType_Name
        //
        // Dapper maps a result column onto a property BY NAME, so QueryAsync<LookupItem> would match
        // nothing at all here: it would return the right number of rows with Id and Name empty on every
        // one, throwing no exception and logging nothing. The two obvious fixes are both worse — aliasing
        // eleven .sql files to fit a C# type inverts which one is the contract, and eleven two-property
        // models differing only in a prefix is not a data layer, it is a rubber stamp.
        //
        // What all eleven DO share is the shape: column 0 is the code, column 1 is the display name. That
        // is what this helper depends on, once, in the open, instead of eleven times by accident. If a
        // twelfth lookup ever returns a third column, it still works — anything past column 1 is ignored.
        // If one ever returns the name FIRST, this is the one place that has to change.
        private async Task<List<LookupItem>> QueryLookupAsync(string procedureName)
        {
            using var connection = _databaseHelper.CreateConnection();
            using var reader = await connection.ExecuteReaderAsync(
                procedureName,
                commandType: CommandType.StoredProcedure);

            var items = new List<LookupItem>();

            while (reader.Read())
            {
                items.Add(new LookupItem
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty,
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1).ToString() ?? string.Empty
                });
            }

            return items;
        }

        public Task<List<LookupItem>> GetDischargeTypesAsync() =>
            QueryLookupAsync("dbo.spLU_DischargeType_List");

        public Task<List<LookupItem>> GetMaritalStatusesAsync() =>
            QueryLookupAsync("dbo.spLU_MaritalStatus_List");

        public Task<List<LookupItem>> GetOccupationsAsync() =>
            QueryLookupAsync("dbo.spLU_Occupation_List");

        public Task<List<LookupItem>> GetOrganizationsAsync() =>
            QueryLookupAsync("dbo.spLU_ORGANIZATION_List");

        public Task<List<LookupItem>> GetPatientDocumentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_PatientDocumentType_List");

        public Task<List<LookupItem>> GetJourneyAppointmentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_PJ_AppType_List");

        public Task<List<LookupItem>> GetRacesAsync() =>
            QueryLookupAsync("dbo.spLU_Race_List");

        public Task<List<LookupItem>> GetReligionsAsync() =>
            QueryLookupAsync("dbo.spLU_Religion_List");

        public Task<List<LookupItem>> GetSourcesAsync() =>
            QueryLookupAsync("dbo.spLU_Source_List");

        public Task<List<LookupItem>> GetStaffDocumentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_STAFFDOCUMENTTYPE_List");

        public Task<List<LookupItem>> GetStaffTypesAsync() =>
            QueryLookupAsync("dbo.spLU_STAFFTYPE_List");

        // The three LU_LOCATION reads map by name like everything else in this file: LocationLookupItem's
        // properties are the procedures' column names.
        public async Task<List<LocationLookupItem>> GetStatesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListStates",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<LocationLookupItem>> GetCitiesByStateAsync(int stateId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListCityByState",
                new { StateId = stateId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<LocationLookupItem>> GetPostcodesByCityAsync(int cityId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListPostcodesByCity",
                new { CityId = cityId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Branch (Admin > Branch) -----

        public async Task<List<BranchDetail>> GetAllBranchesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<BranchDetail>(
                "dbo.spBranch_ListAll",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<BranchDetail?> GetBranchByIdAsync(string branchId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Branch_ID = @Branch_ID: at most one row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<BranchDetail>(
                "dbo.spBranch_GetById",
                new { Branch_ID = branchId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<BranchOption>> GetActiveBranchesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<BranchOption>(
                "dbo.spBranch_ListActive",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<string> CreateBranchAsync(string branchName, string branchLocation, string branchState,
            bool branchStatus, string organizationId, string organizationName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Insert declares @User_ID INT = NULL for its dbo.AuditTrails row: the ACTOR, not a
            // target. DatabaseHelper used to append it automatically off sys.parameters; Dapper cannot, so
            // it is passed explicitly here. Drop it and the insert still succeeds — with
            // AuditTrails.User_Id = 0. See DapperLayerPlan.md's "@User_ID" section and CoreFlow.md §0.1.
            //
            // QuerySingleAsync, not …OrDefault: on a successful run the procedure always ends with
            // `SELECT @Branch_ID AS NewBranch_ID`. Every path that skips that SELECT RAISERRORs first, so
            // the caller gets a SqlException rather than an empty result.
            return await connection.QuerySingleAsync<string>(
                "dbo.spBranch_Insert",
                new
                {
                    Branch_Name = branchName,
                    Branch_Location = branchLocation,
                    Branch_State = branchState,
                    Branch_Status = branchStatus,
                    Organization_ID = organizationId,
                    Organization_Name = organizationName,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateBranchAsync(string branchId, string branchName, string branchLocation,
            string branchState, bool branchStatus, string organizationId, string organizationName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Update declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            await connection.ExecuteAsync(
                "dbo.spBranch_Update",
                new
                {
                    Branch_ID = branchId,
                    Branch_Name = branchName,
                    Branch_Location = branchLocation,
                    Branch_State = branchState,
                    Branch_Status = branchStatus,
                    Organization_ID = organizationId,
                    Organization_Name = organizationName,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task DeleteBranchAsync(string branchId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            await connection.ExecuteAsync(
                "dbo.spBranch_Delete",
                new { Branch_ID = branchId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Users, authentication and lockout -----
        //
        // 🔴 READ THIS BEFORE ADDING A CALL TO THIS BANNER. Five of the nine procedures declare @User_ID,
        // and NOT ONE of them gets _databaseHelper.CurrentUserId. In all five it is `@User_ID INT` with NO
        // DEFAULT, meaning A TARGET USER ROW — the account being read or written — which arrives as an
        // ordinary method argument. The 19 audit-actor procedures are the ones that declare
        // `@User_ID INT = NULL`; that default is the whole tell. Every one of the five below says TARGET on
        // its call, deliberately, because the mistake this prevents is invisible: passing the claim to
        // spUsers_Unlock unlocks the administrator's own account and answers "Account unlocked."
        //
        // None of the nine writes a dbo.AuditTrails row, so there is no audit assertion to make here — the
        // security trail for this area is the Serilog audit channel, written by AccountController.

        public async Task<UserAuthRecord?> GetUserForLoginAsync(string username)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Username = @Username, over a UNIQUE index: at most one row, possibly
            // none. No @User_ID — this procedure reads by username, not by id.
            return await connection.QuerySingleOrDefaultAsync<UserAuthRecord>(
                "dbo.spUsers_ValidateLogin",
                new { Username = username },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<UserAccountRecord?> GetUserByIdAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_GetById declares @User_ID INT with no default: A TARGET — the user row to read. It
            // is the caller's argument and has nothing to do with who is logged in.
            return await connection.QuerySingleOrDefaultAsync<UserAccountRecord>(
                "dbo.spUsers_GetById",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<UserListItem>> GetAllUsersAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<UserListItem>(
                "dbo.spUsers_GetAll",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task RegisterUserAsync(string userName, string username, string userEmail,
            string passwordHash, int userType, string? staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID of either kind: spUsers_Register records no actor and writes no audit row. Who
            // created an account is captured on the Serilog audit channel by the controller instead.
            //
            // staffId is passed as null rather than "" for non-STAFF users. The procedure only inspects it
            // when @User_Type = 3 (where it NULLIFs the blank anyway), so both work today — but null is
            // what the column stores, and "" would be a Staff_ID that matches no staff row.
            await connection.ExecuteAsync(
                "dbo.spUsers_Register",
                new
                {
                    User_Name = userName,
                    Username = username,
                    User_Email = userEmail,
                    PasswordHash = passwordHash,
                    User_Type = userType,
                    Staff_ID = staffId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<FailedLoginResult?> RegisterFailedLoginAsync(string username, int maxFailedAttempts,
            int lockoutMinutes, int attemptWindowMinutes)
        {
            using var connection = _databaseHelper.CreateConnection();

            // THE ONE PROCEDURE THE DAPPER MIGRATION EDITED. It answers through three OUTPUT parameters,
            // which Dapper can only reach via DynamicParameters — a string-keyed bag with a manual
            // .Get<T>("@Name") per value, i.e. exactly the untyped plumbing this layer exists to remove.
            // Prompt 2 therefore APPENDED a trailing SELECT of the same three values to the .sql and KEPT
            // ALL THREE OUTPUT PARAMETERS, so the change is invisible to any caller still using them. The
            // three OUTPUT parameters are simply not sent from here; each declares a default.
            //
            // @NowUtc is likewise not sent, so the procedure uses GETUTCDATE() — the SQL SERVER's clock,
            // not the web server's. The controller compares the returned LockoutEndUtc against the web
            // server's DateTime.UtcNow, so the two machines' clocks must agree for a lockout window to be
            // the length it claims. On one box, as locally, they are the same clock.
            //
            // 🔴 QuerySingleOrDefault, NOT QuerySingle. Two early RETURNs in the procedure skip the new
            // SELECT entirely and emit NO result set: an unknown @Username, and an attempt against an
            // account whose lockout window is already open. Neither is reachable from
            // AccountController.Login — it calls this only after spUsers_ValidateLogin returned a row and
            // after its own lockout check passed — but QuerySingleAsync would throw "Sequence contains no
            // elements" if either ever were, turning a failed login into a 500. Null means "the procedure
            // decided nothing"; the caller treats that as no lockout.
            return await connection.QuerySingleOrDefaultAsync<FailedLoginResult>(
                "dbo.spUsers_RegisterFailedLogin",
                new
                {
                    Username = username,
                    MaxFailedAttempts = maxFailedAttempts,
                    LockoutMinutes = lockoutMinutes,
                    AttemptWindowMinutes = attemptWindowMinutes
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task ResetFailedLoginsAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_ResetFailedLogins declares @User_ID INT with no default: A TARGET — the account whose
            // counters are being cleared, which is the user who just logged in successfully.
            await connection.ExecuteAsync(
                "dbo.spUsers_ResetFailedLogins",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UnlockUserAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 A TARGET, AND THE MOST DANGEROUS ONE IN THE MIGRATION. spUsers_Unlock declares @User_ID INT
            // with no default, and it is THE LOCKED-OUT ACCOUNT BEING UNLOCKED — a SUPERUSER acting on
            // somebody else's row. Writing `User_ID = _databaseHelper.CurrentUserId` here would clear the
            // SUPERUSER's own counters, leave the locked-out user locked, throw nothing, and return
            // "Account unlocked." to the administrator. It is the caller's argument. Do not "tidy" it.
            await connection.ExecuteAsync(
                "dbo.spUsers_Unlock",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_UpdateLastLogin declares @User_ID INT with no default: A TARGET — the user whose
            // Last_Login is being stamped. Note that this runs BEFORE HttpContext.SignInAsync, so
            // DatabaseHelper.CurrentUserId is still null at this point in the login flow anyway.
            await connection.ExecuteAsync(
                "dbo.spUsers_UpdateLastLogin",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateUserPasswordAsync(int userId, string passwordHash)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_UpdatePassword declares @User_ID INT with no default: A TARGET — the account whose
            // hash is being replaced. Today the only caller passes the logged-in user's own id (there is no
            // admin password reset in nucentra), which is exactly why it would be easy to mistake this for
            // an actor parameter. It is not: it is the row in the WHERE clause.
            await connection.ExecuteAsync(
                "dbo.spUsers_UpdatePassword",
                new { User_ID = userId, PasswordHash = passwordHash },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Staff (Admin > Staff) -----

        public async Task<List<StaffListItem>> GetAllStaffAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffListItem>(
                "dbo.spStaff_List",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<StaffDetail?> GetStaffByIdAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Staff_ID = @Staff_ID: at most one row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<StaffDetail>(
                "dbo.spStaff_GetById",
                new { Staff_ID = staffId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string> CreateStaffAsync(StaffSaveInput staff)
        {
            using var connection = _databaseHelper.CreateConnection();

            return await InsertStaffAsync(connection, null, staff, _databaseHelper.CurrentUserId);
        }

        public async Task UpdateStaffAsync(StaffSaveInput staff)
        {
            using var connection = _databaseHelper.CreateConnection();

            await UpdateStaffRowAsync(connection, null, staff, _databaseHelper.CurrentUserId);
        }

        public async Task<StaffDeleteResult> DeleteStaffAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spStaff_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            //
            // TWO RESULT SETS, ALWAYS, IN THIS ORDER — which is the whole reason this is QueryMultipleAsync
            // and not ExecuteAsync, and why the controller used to reach for ExecuteDataSetAsync:
            //
            //   grid 1  one row   Status VARCHAR(20), Message VARCHAR(500)
            //   grid 2  N rows    BlobName VARCHAR(500) — the container keys of the documents just removed
            //
            // Both early-return branches ("NotFound" and "Blocked") emit
            // `SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [BlobName]` before returning, so the GRID COUNT
            // IS STABLE and grid 2 can be read unconditionally. An empty grid 2 means "no storage to
            // reclaim", never "something failed" — only Status says whether the delete happened.
            using var grids = await connection.QueryMultipleAsync(
                "dbo.spStaff_Delete",
                new { Staff_ID = staffId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);

            // ReadSingleAsync into StaffDeleteResult maps Status and Message by name and leaves BlobNames
            // at its initialiser — grid 2 fills it below. Dapper ignores properties it finds no column for.
            var result = await grids.ReadSingleAsync<StaffDeleteResult>();

            var blobNames = await grids.ReadAsync<string?>();

            // The procedure already excludes NULL and blank keys when it captures them, so this filter is
            // belt-and-braces; it is here so the list's element type is honest under nullable reference
            // types rather than carrying nulls a caller would have to re-check.
            result.BlobNames = blobNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();

            return result;
        }

        // 🔴 THE FIRST OF EXACTLY TWO TRANSACTIONAL UNITS OF WORK IN THIS FILE. Everything else here is one
        // method, one procedure; this one runs spStaff_Insert or spStaff_Update, then
        // spStaffDocument_GetById + spStaffDocument_Delete per removed document, then
        // spStaffDocument_Insert per added document — on ONE connection, inside ONE SqlTransaction. The
        // other exception is SaveAppointmentAsync (Prompt 6). Read IDatabaseData for why this is atomic and
        // why the uploads arrive through a callback; this comment covers only the mechanism.
        //
        // The connection is opened and the transaction begun BY HAND here, which is the one thing in this
        // file that does not let Dapper manage the connection: every call below passes `transaction:` and
        // must, because a command on this connection without the transaction throws.
        public async Task<StaffSaveResult> SaveStaffWithDocumentsAsync(
            StaffSaveInput staff,
            IReadOnlyList<int> deleteDocumentIds,
            Func<string, Task<IReadOnlyList<StaffDocumentInput>>> uploadDocumentsAsync)
        {
            var result = new StaffSaveResult();

            // Resolved ONCE, before the transaction opens, and reused for all four audit-actor calls below.
            // It reads the claim off IHttpContextAccessor, which cannot change mid-request — reading it per
            // call would be four identical answers and four chances to forget one.
            var actorUserId = _databaseHelper.CurrentUserId;

            await using var connection = _databaseHelper.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                if (staff.IsNew)
                {
                    result.StaffId = await InsertStaffAsync(connection, transaction, staff, actorUserId);

                    // spStaff_Insert always ends with `SELECT @Staff_ID AS NewStaff_ID` on the path that
                    // inserted, so this cannot fire — but a blank id here would name every blob
                    // "staff//{guid}" and orphan the documents, so it fails loudly rather than continuing.
                    if (string.IsNullOrWhiteSpace(result.StaffId))
                    {
                        throw new InvalidOperationException("Failed to generate Staff ID.");
                    }
                }
                else
                {
                    result.StaffId = staff.Staff_ID;
                    await UpdateStaffRowAsync(connection, transaction, staff, actorUserId);
                }

                // Delete the document ROWS, and capture the blob keys on the way past. Nothing is removed
                // from storage here: the transaction can still roll the rows back, and a deleted blob
                // cannot be un-deleted. The keys travel out on the result for the caller to act on AFTER
                // the commit.
                foreach (var documentId in deleteDocumentIds)
                {
                    var existing = await connection.QuerySingleOrDefaultAsync<StaffDocumentItem>(
                        "dbo.spStaffDocument_GetById",
                        new { StaffDocument_ID = documentId },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);

                    if (existing != null && !string.IsNullOrWhiteSpace(existing.BlobName))
                    {
                        result.RemovedDocuments.Add(new StaffDocumentDeletion
                        {
                            StaffDocument_ID = documentId,
                            BlobName = existing.BlobName
                        });
                    }

                    // spStaffDocument_Delete declares @User_ID INT = NULL: the ACTOR for its
                    // dbo.AuditTrails row. Its @DeletedBlobName OUTPUT parameter is not requested here —
                    // it also declares a default — because the read above already has the key AND the row
                    // is only audited on the Serilog channel by id, which the caller already holds.
                    await connection.ExecuteAsync(
                        "dbo.spStaffDocument_Delete",
                        new { StaffDocument_ID = documentId, User_ID = actorUserId },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);
                }

                // THE CALLBACK. It uploads the files to the private container — which only it can do, since
                // IDocumentStorage lives in CRC.Web — and hands back the rows to insert. It runs HERE,
                // after the staff row exists, because the blob key is staff/{Staff_ID}/{guid}{ext} and on
                // an insert that id was generated three statements ago. Anything it throws propagates into
                // the catch below and rolls the whole transaction back; the caller compensates the blobs.
                var documents = await uploadDocumentsAsync(result.StaffId);

                foreach (var document in documents)
                {
                    await InsertStaffDocumentAsync(connection, transaction, result.StaffId, document, actorUserId);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return result;
        }

        // spStaff_Insert has TWO call sites — CreateStaffAsync and the transaction above — so its fifteen
        // parameters are written out once, here, and cannot drift between them. `transaction` is null on
        // the non-transactional path, which Dapper accepts.
        //
        // spStaff_Insert declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
        // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection. Drop it
        // and the insert still succeeds — with AuditTrails.User_Id = 0.
        //
        // QuerySingleAsync, not …OrDefault: every path that skips the trailing
        // `SELECT @Staff_ID AS NewStaff_ID` RAISERRORs first (a blank @Staff_Type), so the caller gets a
        // SqlException rather than an empty result.
        private static Task<string> InsertStaffAsync(
            SqlConnection connection, SqlTransaction? transaction, StaffSaveInput staff, int? actorUserId)
        {
            return connection.QuerySingleAsync<string>(
                "dbo.spStaff_Insert",
                new
                {
                    staff.Staff_Name,
                    staff.Staff_NRIC,
                    staff.Staff_BirthDate,
                    staff.Staff_Age,
                    staff.Staff_Phone,
                    staff.Staff_Email,
                    staff.Staff_Gender,
                    staff.Staff_ResState,
                    staff.Staff_ResCity,
                    staff.Staff_ResPostcode,
                    staff.Staff_AddLine1,
                    staff.Staff_AddLine2,
                    staff.Staff_Base,
                    staff.Staff_Type,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        // Same arrangement for spStaff_Update, whose parameter list is spStaff_Insert's plus @Staff_ID.
        // It too declares @User_ID INT = NULL — the ACTOR — and it audits only when a row actually
        // changed, so a bad id writes nothing and reports nothing.
        private static Task UpdateStaffRowAsync(
            SqlConnection connection, SqlTransaction? transaction, StaffSaveInput staff, int? actorUserId)
        {
            return connection.ExecuteAsync(
                "dbo.spStaff_Update",
                new
                {
                    staff.Staff_ID,
                    staff.Staff_Name,
                    staff.Staff_NRIC,
                    staff.Staff_BirthDate,
                    staff.Staff_Age,
                    staff.Staff_Phone,
                    staff.Staff_Email,
                    staff.Staff_Gender,
                    staff.Staff_ResState,
                    staff.Staff_ResCity,
                    staff.Staff_ResPostcode,
                    staff.Staff_AddLine1,
                    staff.Staff_AddLine2,
                    staff.Staff_Base,
                    staff.Staff_Type,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        // ----- Staff documents -----

        public async Task<List<StaffDocumentItem>> GetStaffDocumentsAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffDocumentItem>(
                "dbo.spStaffDocument_List",
                new { Staff_ID = staffId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<StaffDocumentItem?> GetStaffDocumentByIdAsync(int documentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE StaffDocument_ID = @StaffDocument_ID over the primary key: at most one
            // row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<StaffDocumentItem>(
                "dbo.spStaffDocument_GetById",
                new { StaffDocument_ID = documentId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task AddStaffDocumentAsync(string staffId, string staffName, string documentTypeId,
            string documentTypeName, string fileName, string blobName, string contentType)
        {
            using var connection = _databaseHelper.CreateConnection();

            await InsertStaffDocumentAsync(
                connection,
                null,
                staffId,
                new StaffDocumentInput
                {
                    Staff_Name = staffName,
                    StaffDocumentType_ID = documentTypeId,
                    StaffDocumentType_Name = documentTypeName,
                    FileName = fileName,
                    BlobName = blobName,
                    ContentType = contentType
                },
                _databaseHelper.CurrentUserId);
        }

        // spStaffDocument_Insert's other two-call-site helper, for the same reason as InsertStaffAsync:
        // AddStaffDocumentAsync runs it on its own, SaveStaffWithDocumentsAsync runs it inside the
        // transaction, and the parameter list is written once.
        //
        // spStaffDocument_Insert declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
        // passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
        private static Task InsertStaffDocumentAsync(
            SqlConnection connection, SqlTransaction? transaction, string staffId,
            StaffDocumentInput document, int? actorUserId)
        {
            return connection.ExecuteAsync(
                "dbo.spStaffDocument_Insert",
                new
                {
                    Staff_ID = staffId,
                    document.Staff_Name,
                    document.StaffDocumentType_ID,
                    document.StaffDocumentType_Name,
                    document.FileName,
                    document.BlobName,
                    document.ContentType,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string?> DeleteStaffDocumentAsync(int documentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 THE ONE PLACE IN THIS FILE THAT USES DynamicParameters, and the only one that should.
            // spStaffDocument_Delete answers through an OUTPUT parameter — @DeletedBlobName VARCHAR(500) —
            // and an OUTPUT parameter is the single thing Dapper cannot reach through an anonymous object.
            // §5.3 records spUsers_RegisterFailedLogin as "the only procedure in nucentra with OUTPUT
            // parameters"; THAT IS WRONG, and this is the second. The difference is what could be done
            // about it: Prompt 2 was allowed to append a trailing SELECT to that procedure, Prompt 3 was
            // not allowed to touch any .sql, and this one has no result set to append to. So the untyped
            // bag stays, confined to four lines, with the name and the type written out where a reader can
            // check them against the procedure.
            //
            // AnsiString, not String: the parameter is VARCHAR(500), and DbType.String would send NVARCHAR
            // for SQL Server to convert back.
            //
            // @User_ID INT = NULL is the ACTOR for the dbo.AuditTrails row, passed explicitly as everywhere
            // else. The procedure audits only when a row was actually deleted.
            var parameters = new DynamicParameters();
            parameters.Add("StaffDocument_ID", documentId);
            parameters.Add("User_ID", _databaseHelper.CurrentUserId);
            parameters.Add("DeletedBlobName", dbType: DbType.AnsiString, size: 500,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "dbo.spStaffDocument_Delete",
                parameters,
                commandType: CommandType.StoredProcedure);

            // NULL means no row was deleted, so there is nothing in storage to remove.
            var deletedBlobName = parameters.Get<string?>("DeletedBlobName");

            return string.IsNullOrWhiteSpace(deletedBlobName) ? null : deletedBlobName;
        }

        public async Task<List<StaffDocumentSetting>> GetStaffDocumentSettingsAsync(string staffTypeId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffDocumentSetting>(
                "dbo.spStaffDocumentSettings_GetByStaffType",
                new { StaffType_ID = staffTypeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // Reuses QueryLookupAsync — the ordinal-reading helper written for the eleven LU_* code procedures
        // — because this procedure has exactly the shape that helper assumes: two columns, the code first
        // (StaffDocumentType_ID) and the display name second (StaffDocumentType_Name). Mapping
        // LookupItem by NAME would match neither column and return empty strings on every row.
        public Task<List<LookupItem>> GetStaffDocumentTypeFiltersAsync() =>
            QueryLookupAsync("dbo.spStaffDocument_LookupDocuments");

        public async Task<List<string>> GetStaffDocumentStaffNamesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // One VARCHAR column, so the row type is the column type. The procedure's WHERE already
            // excludes blank names, and its INNER JOIN excludes documents whose Staff_ID matches no staff.
            var results = await connection.QueryAsync<string>(
                "dbo.spStaffDocument_StaffNames",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
